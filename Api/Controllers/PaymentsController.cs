using System.Text.Json;
using Api.Contracts.Payments;
using Api.Extensions;
using Api.Options;
using Api.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;

namespace Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly ProductDbContext _dbContext;
    private readonly StripePaymentService _stripe;
    private readonly OrderPaymentLifecycleService _lifecycle;
    private readonly StripeOptions _stripeOptions;

    public PaymentsController(
        ProductDbContext dbContext,
        StripePaymentService stripe,
        OrderPaymentLifecycleService lifecycle,
        IOptions<StripeOptions> stripeOptions)
    {
        _dbContext = dbContext;
        _stripe = stripe;
        _lifecycle = lifecycle;
        _stripeOptions = stripeOptions.Value;
    }

    [Authorize]
    [HttpGet("config")]
    public ActionResult<StripePublicConfigResponse> GetStripeConfig()
    {
        return Ok(new StripePublicConfigResponse
        {
            PublishableKey = _stripeOptions.PublishableKey
        });
    }

    [Authorize]
    [HttpPost("payment-intent")]
    public async Task<ActionResult<CreatePaymentIntentResponse>> CreatePaymentIntent(
        [FromBody] CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        var buyerId = User.GetRequiredUserId();
        var buyer = await _dbContext.Users.FirstOrDefaultAsync(user => user.Id == buyerId, cancellationToken)
            ?? throw new KeyNotFoundException("Buyer was not found.");

        var draft = await BuildDraftAsync(buyerId, request, cancellationToken);
        var paymentSession = new PaymentSession
        {
            BuyerId = buyerId,
            StripeCheckoutSessionId = $"payment-intent-pending-{Guid.NewGuid():N}",
            TotalAmount = draft.Items.Sum(item => item.UnitPrice * item.Quantity),
            DraftJson = JsonSerializer.Serialize(draft),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.PaymentSessions.Add(paymentSession);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var paymentIntent = await _stripe.CreatePaymentIntentAsync(paymentSession, draft, buyer, cancellationToken);
            paymentSession.StripeCheckoutSessionId = paymentIntent.Id;
            paymentSession.StripePaymentIntentId = paymentIntent.Id;
            paymentSession.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new CreatePaymentIntentResponse
            {
                PaymentIntentId = paymentIntent.Id,
                ClientSecret = paymentIntent.ClientSecret,
                PublishableKey = _stripeOptions.PublishableKey
            });
        }
        catch
        {
            paymentSession.Status = PaymentSessionStatus.Failed;
            paymentSession.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    [Authorize]
    [HttpPost("checkout-session")]
    public async Task<ActionResult<CreateCheckoutSessionResponse>> CreateCheckoutSession(
        [FromBody] CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        var buyerId = User.GetRequiredUserId();
        var buyer = await _dbContext.Users.FirstOrDefaultAsync(user => user.Id == buyerId, cancellationToken)
            ?? throw new KeyNotFoundException("Buyer was not found.");

        var draft = await BuildDraftAsync(buyerId, request, cancellationToken);
        var paymentSession = new PaymentSession
        {
            BuyerId = buyerId,
            StripeCheckoutSessionId = $"pending-{Guid.NewGuid():N}",
            TotalAmount = draft.Items.Sum(item => item.UnitPrice * item.Quantity),
            DraftJson = JsonSerializer.Serialize(draft),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.PaymentSessions.Add(paymentSession);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var stripeSession = await _stripe.CreateCheckoutSessionAsync(paymentSession, draft, buyer, cancellationToken);
            paymentSession.StripeCheckoutSessionId = stripeSession.Id;
            paymentSession.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new CreateCheckoutSessionResponse
            {
                SessionId = stripeSession.Id,
                Url = stripeSession.Url
            });
        }
        catch
        {
            paymentSession.Status = PaymentSessionStatus.Failed;
            paymentSession.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    [Authorize]
    [HttpPost("connect/onboarding-session")]
    public async Task<ActionResult<ConnectOnboardingResponse>> CreateConnectOnboardingSession(
        CancellationToken cancellationToken)
    {
        var sellerId = User.GetRequiredUserId();
        var seller = await _dbContext.Users.FirstOrDefaultAsync(user => user.Id == sellerId, cancellationToken)
            ?? throw new KeyNotFoundException("Seller was not found.");

        (string ClientSecret, bool IsComplete) result;
        try
        {
            result = await _stripe.CreateConnectOnboardingSessionAsync(seller, cancellationToken);
        }
        catch (StripeException exception)
        {
            var stripeMessage = exception.StripeError?.Message ?? exception.Message;
            throw new InvalidOperationException($"Stripe Connect onboarding is unavailable: {stripeMessage}");
        }
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ConnectOnboardingResponse
        {
            ClientSecret = result.ClientSecret,
            AccountId = seller.StripeConnectAccountId!,
            PublishableKey = _stripeOptions.PublishableKey,
            IsComplete = result.IsComplete
        });
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_stripeOptions.WebhookSecret))
        {
            return Problem("Stripe webhook secret is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, _stripeOptions.WebhookSecret);
        }
        catch (Exception exception) when (exception is StripeException or ArgumentException)
        {
            return BadRequest("Invalid Stripe webhook signature.");
        }

        if (stripeEvent.Type is "payment_intent.succeeded")
        {
            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
            if (paymentIntent is not null)
            {
                await _lifecycle.ProcessPaymentIntentSucceededAsync(paymentIntent, cancellationToken);
            }
        }
        else if (stripeEvent.Type is "checkout.session.completed" or "checkout.session.async_payment_succeeded")
        {
            var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
            if (session is not null)
            {
                await _lifecycle.ProcessCheckoutCompletedAsync(session, cancellationToken);
            }
        }

        return Ok();
    }

    private async Task<CheckoutPaymentDraft> BuildDraftAsync(
        Guid buyerId,
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        var items = new List<CheckoutPaymentDraftItem>();
        var cartItemIds = request.CartItemIds.Distinct().ToList();

        if (request.ListingId.HasValue)
        {
            if (cartItemIds.Count > 0)
            {
                throw new InvalidOperationException("Choose either a listing or cart items for checkout.");
            }

            var listing = await _dbContext.Listings
                .Include(item => item.Owner)
                .Include(item => item.Images)
                .FirstOrDefaultAsync(item => item.Id == request.ListingId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Listing was not found.");

            ValidateListingForCheckout(listing, buyerId, request.Quantity);
            items.Add(ToDraftItem(listing, request.Quantity));
        }
        else
        {
            if (cartItemIds.Count == 0)
            {
                throw new InvalidOperationException("Choose at least one cart item for checkout.");
            }

            var cartItems = await _dbContext.CartItems
                .Include(item => item.Listing!)
                    .ThenInclude(listing => listing.Owner)
                .Include(item => item.Listing!)
                    .ThenInclude(listing => listing.Images)
                .Where(item => item.UserId == buyerId && cartItemIds.Contains(item.Id) && item.ListingId != null)
                .ToListAsync(cancellationToken);

            if (cartItems.Count != cartItemIds.Count)
            {
                throw new InvalidOperationException("One or more selected cart items are no longer available.");
            }

            foreach (var cartItem in cartItems)
            {
                ValidateListingForCheckout(cartItem.Listing!, buyerId, cartItem.Quantity);
                items.Add(ToDraftItem(cartItem.Listing!, cartItem.Quantity));
            }
        }

        var cancelUrl = request.ListingId.HasValue
            ? $"{_stripeOptions.FrontendBaseUrl.TrimEnd('/')}/checkout/listing/{request.ListingId.Value}?payment=cancelled"
            : $"{_stripeOptions.FrontendBaseUrl.TrimEnd('/')}/checkout/cart?payment=cancelled";

        return new CheckoutPaymentDraft
        {
            BuyerId = buyerId,
            BuyerFirstName = request.BuyerFirstName.Trim(),
            BuyerLastName = request.BuyerLastName.Trim(),
            BuyerEmail = request.BuyerEmail.Trim(),
            BuyerPhone = request.BuyerPhone.Trim(),
            DeliveryAddress = request.DeliveryAddress.Trim(),
            DeliveryCity = request.DeliveryCity.Trim(),
            SaveCard = request.SaveCard,
            CancelUrl = cancelUrl,
            CartItemIds = cartItemIds,
            Items = items
        };
    }

    private static CheckoutPaymentDraftItem ToDraftItem(Listing listing, int quantity)
    {
        return new CheckoutPaymentDraftItem
        {
            ListingId = listing.Id,
            SellerId = listing.OwnerId,
            Quantity = quantity,
            UnitPrice = listing.Price,
            Title = listing.Title,
            ImageUrl = listing.Images
                .OrderBy(image => image.SortOrder)
                .Select(image => image.Url)
                .FirstOrDefault()
        };
    }

    private static void ValidateListingForCheckout(Listing listing, Guid buyerId, int quantity)
    {
        if (listing.OwnerId == buyerId)
        {
            throw new InvalidOperationException("You cannot order your own listing.");
        }

        if (quantity < 1 || InventoryService.GetAvailableQuantity(listing.Quantity, listing.ReservedQuantity) < quantity)
        {
            throw new InvalidOperationException("The requested quantity is no longer available.");
        }

        if (listing.Status is not (ListingStatus.Active or ListingStatus.Published))
        {
            throw new InvalidOperationException("This listing is no longer available for purchase.");
        }
    }
}
