using Api.Contracts.Orders;
using Api.Extensions;
using Api.Mappers;
using Api.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StripeException = Stripe.StripeException;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly ProductDbContext _dbContext;
    private readonly InventoryService _inventory;
    private readonly OrderPaymentLifecycleService _lifecycle;
    private readonly OrderRealtimeNotifier _orderRealtime;
    private readonly StripePaymentService _stripe;

    public OrdersController(
        ProductDbContext dbContext,
        InventoryService inventory,
        OrderPaymentLifecycleService lifecycle,
        OrderRealtimeNotifier orderRealtime,
        StripePaymentService stripe)
    {
        _dbContext = dbContext;
        _inventory = inventory;
        _lifecycle = lifecycle;
        _orderRealtime = orderRealtime;
        _stripe = stripe;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyCollection<OrderSummaryResponse>>> GetMyOrders(
        [FromQuery] bool asSeller = false,
        [FromQuery] Guid? userId = null)
    {
        var currentUserId = User.GetRequiredUserId();
        var targetUserId = userId.HasValue && User.IsElevatedAdmin() ? userId.Value : currentUserId;
        var orders = await LoadOrdersSummaryQuery(targetUserId, asSeller).ToListAsync();
        return Ok(orders.Select(order => MarketplaceMapper.MapOrderSummary(order, targetUserId)).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDetailsResponse>> GetOrderById(Guid id)
    {
        var userId = User.GetRequiredUserId();
        var order = await LoadOrderAsync(id);
        if (!CanAccessOrder(order, userId))
        {
            return Forbid();
        }

        return Ok(MarketplaceMapper.MapOrderDetails(order));
    }

    [HttpPost]
    public async Task<ActionResult<OrderDetailsResponse>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var buyerId = User.GetRequiredUserId();
        var listing = await LoadListingForOrderAsync(request.ListingId);
        EnsureCashOnDeliveryOnly(request.PaymentMethod);
        await EnsureCanPurchaseListingAsync(listing, buyerId, request.Quantity);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        if (!await _inventory.TryReserveAsync(listing.Id, request.Quantity))
        {
            throw new InvalidOperationException("The requested quantity is no longer available.");
        }

        var order = CreateOrderEntity(listing, buyerId, request);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        await UpdateOwnerListingCountAsync(listing.OwnerId);

        var created = await LoadOrderAsync(order.Id);
        await _orderRealtime.NotifyAsync(created, "created");
        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, MarketplaceMapper.MapOrderDetails(created));
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutCartResponse>> CheckoutCart([FromBody] CheckoutCartRequest request)
    {
        var buyerId = User.GetRequiredUserId();
        var requestedCartItemIds = request.CartItemIds.Distinct().ToArray();

        var cartItemsQuery = _dbContext.CartItems
            .Include(item => item.Listing!)
                .ThenInclude(listing => listing.Owner)
            .Include(item => item.Listing!)
                .ThenInclude(listing => listing.Images)
            .Where(item => item.UserId == buyerId && item.ListingId != null);

        if (requestedCartItemIds.Length > 0)
        {
            cartItemsQuery = cartItemsQuery.Where(item => requestedCartItemIds.Contains(item.Id));
        }
        else
        {
            cartItemsQuery = cartItemsQuery.Where(item => item.IsSelected);
        }

        var cartItems = await cartItemsQuery.ToListAsync();
        if (cartItems.Count == 0)
        {
            throw new InvalidOperationException("Choose at least one cart item for checkout.");
        }

        EnsureCashOnDeliveryOnly(request.PaymentMethod);

        foreach (var cartItem in cartItems)
        {
            await EnsureCanPurchaseListingAsync(cartItem.Listing!, buyerId, cartItem.Quantity);
        }

        var createdOrders = new List<Order>();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        foreach (var cartItem in cartItems.OrderBy(item => item.ListingId))
        {
            if (!await _inventory.TryReserveAsync(cartItem.ListingId!.Value, cartItem.Quantity))
            {
                throw new InvalidOperationException("The requested quantity is no longer available.");
            }

            createdOrders.Add(CreateOrderEntity(cartItem.Listing!, buyerId, request, cartItem.Quantity));
        }
        _dbContext.Orders.AddRange(createdOrders);
        _dbContext.CartItems.RemoveRange(cartItems);
        await _dbContext.SaveChangesAsync();
        foreach (var ownerId in cartItems.Select(item => item.Listing!.OwnerId).Distinct())
        {
            await UpdateOwnerListingCountAsync(ownerId);
        }
        await transaction.CommitAsync();

        var orderIds = createdOrders.Select(order => order.Id).ToArray();
        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => orderIds.Contains(order.Id))
            .Include(order => order.Listing)
                .ThenInclude(listing => listing.Category)
            .Include(order => order.Listing)
                .ThenInclude(listing => listing.Owner)
            .Include(order => order.Listing)
                .ThenInclude(listing => listing.Images)
            .Include(order => order.Buyer)
                .ThenInclude(user => user.ReceivedReviews)
            .Include(order => order.Seller)
                .ThenInclude(user => user.ReceivedReviews)
            .Include(order => order.Shipment)
            .ToListAsync();

        foreach (var createdOrder in orders)
        {
            await _orderRealtime.NotifyAsync(createdOrder, "created");
        }

        return Ok(new CheckoutCartResponse
        {
            Orders = orders.Select(MarketplaceMapper.MapOrderDetails).ToArray(),
            TotalAmount = orders.Sum(order => order.TotalAmount)
        });
    }

    [HttpPatch("{id:guid}/decision")]
    public async Task<ActionResult<OrderDetailsResponse>> SetSellerDecision(Guid id, [FromBody] UpdateOrderDecisionRequest request)
    {
        var userId = User.GetRequiredUserId();
        var order = await _dbContext.Orders
            .Include(item => item.Listing)
            .Include(item => item.Shipment)
            .Include(item => item.Seller)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.SellerId != userId && !User.IsElevatedAdmin())
        {
            throw new UnauthorizedAccessException("You cannot manage this order.");
        }

        if (order.Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException("This order is no longer awaiting a seller decision.");
        }

        if (request.Approve && order.PaymentMethod == PaymentMethod.Card &&
            !await CanSellerReceiveTransfersAsync(order.Seller))
        {
            throw new InvalidOperationException("Connect your Stripe payout account before accepting a card order.");
        }

        if (request.Approve)
        {
            await EnsureAcceptedQuantityAvailableAsync(order);
        }

        order.Status = request.Approve ? OrderStatus.Accepted : OrderStatus.Declined;
        order.UpdatedAtUtc = DateTime.UtcNow;

        if (order.Shipment is not null)
        {
            order.Shipment.Status = request.Approve ? ShipmentStatus.ReadyToShip : ShipmentStatus.Cancelled;
            order.Shipment.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
        if (!request.Approve)
        {
            await _lifecycle.RefundAsync(order);
        }
        await UpdateOwnerListingCountAsync(order.Listing.OwnerId);

        var updated = await LoadOrderAsync(order.Id);
        await _orderRealtime.NotifyAsync(updated);
        return Ok(MarketplaceMapper.MapOrderDetails(updated));
    }

    [HttpPatch("{id:guid}/cancel")]
    public async Task<ActionResult<OrderDetailsResponse>> CancelOrder(Guid id, [FromBody] CancelOrderRequest request)
    {
        var userId = User.GetRequiredUserId();
        var order = await _dbContext.Orders
            .Include(item => item.Listing)
            .Include(item => item.Shipment)
            .Include(item => item.Seller)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.BuyerId != userId && !User.IsElevatedAdmin())
        {
            throw new UnauthorizedAccessException("You cannot cancel this order.");
        }

        if (order.Status is OrderStatus.Delivered or OrderStatus.Declined or OrderStatus.Cancelled or OrderStatus.Expired)
        {
            throw new InvalidOperationException("This order can no longer be cancelled.");
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAtUtc = DateTime.UtcNow;

        if (order.Shipment is not null)
        {
            order.Shipment.Status = ShipmentStatus.Cancelled;
            order.Shipment.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
        await _lifecycle.RefundAsync(order);
        await UpdateOwnerListingCountAsync(order.Listing.OwnerId);

        var updated = await LoadOrderAsync(order.Id);
        await _orderRealtime.NotifyAsync(updated);
        return Ok(MarketplaceMapper.MapOrderDetails(updated));
    }

    [HttpPatch("{id:guid}/shipment")]
    public async Task<ActionResult<OrderDetailsResponse>> UpdateShipment(Guid id, [FromBody] UpdateShipmentRequest request)
    {
        var userId = User.GetRequiredUserId();
        var order = await _dbContext.Orders
            .Include(item => item.Listing)
            .Include(item => item.Shipment)
            .Include(item => item.Seller)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.SellerId != userId && !User.IsElevatedAdmin())
        {
            throw new UnauthorizedAccessException("You cannot update shipment for this order.");
        }

        var shipment = order.Shipment ?? throw new InvalidOperationException("Shipment record is missing.");
        ValidateShipmentTransition(order, request.Status);

        if (request.Status == ShipmentStatus.ReadyToShip &&
            order.PaymentMethod == PaymentMethod.Card &&
            !await CanSellerReceiveTransfersAsync(order.Seller))
        {
            throw new InvalidOperationException("Connect your Stripe payout account before confirming a card order.");
        }

        if (request.Status == ShipmentStatus.ReadyToShip)
        {
            await EnsureAcceptedQuantityAvailableAsync(order);
        }

        shipment.Status = request.Status;
        shipment.SenderFirstName = request.SenderFirstName.Trim();
        shipment.SenderLastName = request.SenderLastName.Trim();
        shipment.SenderPhone = request.SenderPhone.Trim();
        shipment.PayoutCardMasked = order.PaymentMethod == PaymentMethod.CashOnDelivery
            ? MaskCardNumber(request.PayoutCardMasked)
            : string.Empty;
        shipment.UpdatedAtUtc = DateTime.UtcNow;

        switch (request.Status)
        {
            case ShipmentStatus.ReadyToShip:
                order.Status = OrderStatus.Accepted;
                break;
            case ShipmentStatus.InDelivery:
                order.Status = OrderStatus.Shipped;
                shipment.ShippedAtUtc ??= DateTime.UtcNow;
                break;
            case ShipmentStatus.Arrived:
                order.Status = OrderStatus.ReadyForPickup;
                order.ReadyForPickupAtUtc ??= DateTime.UtcNow;
                shipment.ArrivedAtUtc ??= DateTime.UtcNow;
                break;
        }

        order.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        await UpdateOwnerListingCountAsync(order.Listing.OwnerId);

        var updated = await LoadOrderAsync(order.Id);
        await _orderRealtime.NotifyAsync(updated);
        return Ok(MarketplaceMapper.MapOrderDetails(updated));
    }

    [HttpPatch("{id:guid}/confirm-receipt")]
    public async Task<ActionResult<OrderDetailsResponse>> ConfirmReceipt(Guid id)
    {
        var userId = User.GetRequiredUserId();
        var order = await _dbContext.Orders
            .Include(item => item.Listing)
            .Include(item => item.Seller)
            .Include(item => item.Shipment)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.BuyerId != userId && !User.IsElevatedAdmin())
        {
            throw new UnauthorizedAccessException("Only the buyer can confirm receipt.");
        }

        if (order.Status != OrderStatus.ReadyForPickup)
        {
            throw new InvalidOperationException("This order is not ready for pickup.");
        }

        await _lifecycle.MarkDeliveredAndTransferAsync(order);
        await UpdateOwnerListingCountAsync(order.Listing.OwnerId);

        var updated = await LoadOrderAsync(order.Id);
        await _orderRealtime.NotifyAsync(updated);
        return Ok(MarketplaceMapper.MapOrderDetails(updated));
    }

    private IQueryable<Order> LoadOrdersSummaryQuery(Guid userId, bool asSeller)
    {
        return _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Listing)
                .ThenInclude(listing => listing.Images)
            .Include(order => order.Buyer)
            .Include(order => order.Seller)
            .Include(order => order.Shipment)
            .Include(order => order.Reviews)
            .Where(order => asSeller ? order.SellerId == userId : order.BuyerId == userId)
            .OrderByDescending(order => order.CreatedAtUtc);
    }

    private async Task<Order> LoadOrderAsync(Guid id)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Listing)
                .ThenInclude(listing => listing.Category)
            .Include(order => order.Listing)
                .ThenInclude(listing => listing.Owner)
            .Include(order => order.Listing)
                .ThenInclude(listing => listing.Images)
            .Include(order => order.Buyer)
                .ThenInclude(user => user.ReceivedReviews)
            .Include(order => order.Seller)
                .ThenInclude(user => user.ReceivedReviews)
            .Include(order => order.Shipment)
            .FirstOrDefaultAsync(order => order.Id == id)
            ?? throw new KeyNotFoundException("Order was not found.");
    }

    private async Task<Listing> LoadListingForOrderAsync(Guid listingId)
    {
        return await _dbContext.Listings
            .Include(item => item.Owner)
            .Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.Id == listingId)
            ?? throw new KeyNotFoundException("Listing was not found.");
    }

    private bool CanAccessOrder(Order order, Guid currentUserId)
    {
        return order.BuyerId == currentUserId ||
               order.SellerId == currentUserId ||
               User.IsElevatedAdmin();
    }

    private async Task EnsureCanPurchaseListingAsync(Listing listing, Guid buyerId, int requestedQuantity)
    {
        if (listing.OwnerId == buyerId)
        {
            throw new InvalidOperationException("You cannot order your own listing.");
        }

        if (requestedQuantity < 1)
        {
            throw new InvalidOperationException("Order quantity must be at least 1.");
        }

        if (InventoryService.GetAvailableQuantity(listing.Quantity, listing.ReservedQuantity) < 1)
        {
            throw new InvalidOperationException("This listing is out of stock.");
        }

        if (listing.Status is ListingStatus.Sold or ListingStatus.InDelivery or ListingStatus.Arrived or ListingStatus.Archived or ListingStatus.Inactive or ListingStatus.Blocked or ListingStatus.Draft)
        {
            throw new InvalidOperationException("This listing is no longer available for purchase.");
        }

        if (InventoryService.GetAvailableQuantity(listing.Quantity, listing.ReservedQuantity) < requestedQuantity)
        {
            throw new InvalidOperationException("The requested quantity is no longer available.");
        }
    }

    private Order CreateOrderEntity(Listing listing, Guid buyerId, CreateOrderRequest request)
    {
        var primaryImageUrl = listing.Images
            .OrderBy(image => image.SortOrder)
            .Select(image => image.Url)
            .FirstOrDefault();

        return new Order
        {
            ListingId = listing.Id,
            BuyerId = buyerId,
            SellerId = listing.OwnerId,
            PaymentMethod = request.PaymentMethod,
            PaymentState = PaymentState.NotApplicable,
            InventoryReserved = true,
            Quantity = request.Quantity,
            PaymentCardMasked = null,
            TotalAmount = listing.Price * request.Quantity,
            ListingTitleSnapshot = listing.Title,
            ListingPrimaryImageUrlSnapshot = primaryImageUrl,
            BuyerFirstName = request.BuyerFirstName.Trim(),
            BuyerLastName = request.BuyerLastName.Trim(),
            BuyerEmail = request.BuyerEmail.Trim(),
            BuyerPhone = request.BuyerPhone.Trim(),
            DeliveryAddress = request.DeliveryAddress.Trim(),
            DeliveryCity = request.DeliveryCity.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            SellerDecisionDeadlineUtc = OrderPaymentLifecycleService.GetSellerDecisionDeadline(DateTime.UtcNow),
            Shipment = new Shipment
            {
                RecipientFirstName = request.BuyerFirstName.Trim(),
                RecipientLastName = request.BuyerLastName.Trim(),
                RecipientPhone = request.BuyerPhone.Trim(),
                DeliveryAddress = request.DeliveryAddress.Trim(),
                DeliveryCity = request.DeliveryCity.Trim(),
                Status = ShipmentStatus.Pending,
                SenderFirstName = listing.Owner.UserName ?? string.Empty,
                SenderLastName = string.Empty,
                SenderPhone = listing.Owner.PhoneNumber ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }
        };
    }

    private Order CreateOrderEntity(Listing listing, Guid buyerId, CheckoutCartRequest request)
    {
        return CreateOrderEntity(listing, buyerId, request, 1);
    }

    private Order CreateOrderEntity(Listing listing, Guid buyerId, CheckoutCartRequest request, int quantity)
    {
        return CreateOrderEntity(listing, buyerId, new CreateOrderRequest
        {
            ListingId = listing.Id,
            Quantity = quantity,
            PaymentMethod = request.PaymentMethod,
            BuyerFirstName = request.BuyerFirstName,
            BuyerLastName = request.BuyerLastName,
            BuyerEmail = request.BuyerEmail,
            BuyerPhone = request.BuyerPhone,
            DeliveryAddress = request.DeliveryAddress,
            DeliveryCity = request.DeliveryCity
        });
    }

    private static void EnsureCashOnDeliveryOnly(PaymentMethod paymentMethod)
    {
        if (paymentMethod == PaymentMethod.CashOnDelivery)
        {
            return;
        }

        throw new InvalidOperationException("Card payments must be completed through the secure Stripe payment form.");
    }

    private static string MaskCardNumber(string? cardNumber)
    {
        var digits = NormalizeDigits(cardNumber);
        if (digits.Length < 4)
        {
            return string.Empty;
        }

        return $"**** **** **** {digits[^4..]}";
    }

    private static string NormalizeDigits(string? value)
    {
        return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    private static void ValidateShipmentTransition(Order order, ShipmentStatus nextStatus)
    {
        var currentStatus = order.Shipment?.Status ?? ShipmentStatus.Pending;
        var isValid = nextStatus switch
        {
            ShipmentStatus.ReadyToShip => currentStatus == ShipmentStatus.Pending && order.Status == OrderStatus.Pending,
            ShipmentStatus.InDelivery => currentStatus == ShipmentStatus.ReadyToShip && order.Status == OrderStatus.Accepted,
            ShipmentStatus.Arrived => currentStatus == ShipmentStatus.InDelivery && order.Status == OrderStatus.Shipped,
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidOperationException("Order status must move forward one step at a time.");
        }
    }

    private async Task EnsureAcceptedQuantityAvailableAsync(Order order)
    {
        var acceptedQuantity = await _dbContext.Orders
            .Where(item =>
                item.ListingId == order.ListingId &&
                item.Id != order.Id &&
                (item.Status == OrderStatus.Accepted ||
                 item.Status == OrderStatus.Shipped ||
                 item.Status == OrderStatus.ReadyForPickup))
            .SumAsync(item => (int?)item.Quantity) ?? 0;

        if (acceptedQuantity + order.Quantity > order.Listing.Quantity)
        {
            throw new InvalidOperationException("The seller has already accepted the available quantity for this listing.");
        }
    }

    private async Task UpdateOwnerListingCountAsync(Guid ownerId)
    {
        var activeListingsCount = await _dbContext.Listings.CountAsync(listing =>
            listing.OwnerId == ownerId &&
            (listing.Status == ListingStatus.Published || listing.Status == ListingStatus.Active));

        var owner = await _dbContext.Users.FirstOrDefaultAsync(user => user.Id == ownerId);
        if (owner is null)
        {
            return;
        }

        owner.ActiveListingsCount = activeListingsCount;
        await _dbContext.SaveChangesAsync();
    }

    private async Task<bool> CanSellerReceiveTransfersAsync(Domain.IdentityEntities.User seller)
    {
        try
        {
            return await _stripe.CanReceiveTransfersAsync(seller);
        }
        catch (StripeException exception)
        {
            var stripeMessage = exception.StripeError?.Message ?? exception.Message;
            throw new InvalidOperationException($"Stripe Connect payout account is unavailable: {stripeMessage}");
        }
    }
}
