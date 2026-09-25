using Api.Contracts.Orders;
using Api.Extensions;
using Api.Mappers;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly ProductDbContext _dbContext;

    public OrdersController(ProductDbContext dbContext)
    {
        _dbContext = dbContext;
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
        EnsureCanAccessOrder(order, userId);
        return Ok(MarketplaceMapper.MapOrderDetails(order));
    }

    [HttpPost]
    public async Task<ActionResult<OrderDetailsResponse>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var buyerId = User.GetRequiredUserId();
        var listing = await LoadListingForOrderAsync(request.ListingId);
        ValidatePaymentDetails(request.PaymentMethod, request.CardNumber, request.CardExpiry, request.CardCvc);
        await EnsureCanPurchaseListingAsync(listing, buyerId, request.Quantity);

        var order = CreateOrderEntity(listing, buyerId, request);
        await ApplyReservationStateAsync(listing, request.Quantity);

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        await UpdateOwnerListingCountAsync(listing.OwnerId);

        var created = await LoadOrderAsync(order.Id);
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

        ValidatePaymentDetails(request.PaymentMethod, request.CardNumber, request.CardExpiry, request.CardCvc);

        foreach (var cartItem in cartItems)
        {
            await EnsureCanPurchaseListingAsync(cartItem.Listing!, buyerId, cartItem.Quantity);
        }

        var createdOrders = new List<Order>();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        foreach (var cartItem in cartItems)
        {
            createdOrders.Add(CreateOrderEntity(cartItem.Listing!, buyerId, request, cartItem.Quantity));
            await ApplyReservationStateAsync(cartItem.Listing!, cartItem.Quantity);
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
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.SellerId != userId && !User.IsElevatedAdmin())
        {
            throw new UnauthorizedAccessException("You cannot manage this order.");
        }

        order.Status = request.Approve ? OrderStatus.Accepted : OrderStatus.Declined;
        order.UpdatedAtUtc = DateTime.UtcNow;

        if (order.Shipment is not null)
        {
            order.Shipment.Status = request.Approve ? ShipmentStatus.ReadyToShip : ShipmentStatus.Cancelled;
            order.Shipment.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
        await RecalculateListingStatusAsync(order.Listing);
        await _dbContext.SaveChangesAsync();
        await UpdateOwnerListingCountAsync(order.Listing.OwnerId);

        var updated = await LoadOrderAsync(order.Id);
        return Ok(MarketplaceMapper.MapOrderDetails(updated));
    }

    [HttpPatch("{id:guid}/cancel")]
    public async Task<ActionResult<OrderDetailsResponse>> CancelOrder(Guid id, [FromBody] CancelOrderRequest request)
    {
        var userId = User.GetRequiredUserId();
        var order = await _dbContext.Orders
            .Include(item => item.Listing)
            .Include(item => item.Shipment)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.BuyerId != userId && !User.IsElevatedAdmin())
        {
            throw new UnauthorizedAccessException("You cannot cancel this order.");
        }

        if (order.Status is OrderStatus.Delivered or OrderStatus.Declined or OrderStatus.Cancelled)
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
        await RecalculateListingStatusAsync(order.Listing);
        await _dbContext.SaveChangesAsync();
        await UpdateOwnerListingCountAsync(order.Listing.OwnerId);

        var updated = await LoadOrderAsync(order.Id);
        return Ok(MarketplaceMapper.MapOrderDetails(updated));
    }

    [HttpPatch("{id:guid}/shipment")]
    public async Task<ActionResult<OrderDetailsResponse>> UpdateShipment(Guid id, [FromBody] UpdateShipmentRequest request)
    {
        var userId = User.GetRequiredUserId();
        var order = await _dbContext.Orders
            .Include(item => item.Listing)
            .Include(item => item.Shipment)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.SellerId != userId && order.BuyerId != userId && !User.IsElevatedAdmin())
        {
            throw new UnauthorizedAccessException("You cannot update shipment for this order.");
        }

        var shipment = order.Shipment ?? throw new InvalidOperationException("Shipment record is missing.");
        shipment.Status = request.Status;
        shipment.SenderFirstName = request.SenderFirstName.Trim();
        shipment.SenderLastName = request.SenderLastName.Trim();
        shipment.SenderPhone = request.SenderPhone.Trim();
        shipment.PayoutCardMasked = request.PayoutCardMasked.Trim();
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
                order.Status = OrderStatus.Delivered;
                shipment.ArrivedAtUtc ??= DateTime.UtcNow;
                ApplyDeliveredListingState(order.Listing, order.Quantity);
                break;
            case ShipmentStatus.Cancelled:
                order.Status = OrderStatus.Cancelled;
                break;
        }

        order.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        await RecalculateListingStatusAsync(order.Listing);
        await _dbContext.SaveChangesAsync();
        await UpdateOwnerListingCountAsync(order.Listing.OwnerId);

        var updated = await LoadOrderAsync(order.Id);
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

    private void EnsureCanAccessOrder(Order order, Guid currentUserId)
    {
        if (order.BuyerId != currentUserId && order.SellerId != currentUserId && !User.IsElevatedAdmin())
        {
            throw new UnauthorizedAccessException("You cannot access this order.");
        }
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

        if (listing.Quantity < 1)
        {
            throw new InvalidOperationException("This listing is out of stock.");
        }

        if (listing.Status is ListingStatus.Sold or ListingStatus.InDelivery or ListingStatus.Arrived or ListingStatus.Archived or ListingStatus.Inactive or ListingStatus.Blocked or ListingStatus.Draft)
        {
            throw new InvalidOperationException("This listing is no longer available for purchase.");
        }

        var reservedQuantity = await _dbContext.Orders
            .Where(order =>
            order.ListingId == listing.Id &&
            order.Status != OrderStatus.Delivered &&
            order.Status != OrderStatus.Declined &&
            order.Status != OrderStatus.Cancelled)
            .SumAsync(order => (int?)order.Quantity) ?? 0;

        if (listing.Quantity - reservedQuantity < requestedQuantity)
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
            Quantity = request.Quantity,
            PaymentCardMasked = MaskCardNumber(request.CardNumber),
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
            CardNumber = request.CardNumber,
            CardExpiry = request.CardExpiry,
            CardCvc = request.CardCvc,
            BuyerFirstName = request.BuyerFirstName,
            BuyerLastName = request.BuyerLastName,
            BuyerEmail = request.BuyerEmail,
            BuyerPhone = request.BuyerPhone,
            DeliveryAddress = request.DeliveryAddress,
            DeliveryCity = request.DeliveryCity
        });
    }

    private static void ApplyDeliveredListingState(Listing listing, int soldQuantity)
    {
        if (listing.Quantity <= soldQuantity)
        {
            listing.Quantity = 0;
            listing.Status = ListingStatus.Inactive;
            return;
        }

        listing.Quantity -= soldQuantity;
        listing.Status = ListingStatus.Published;
    }

    private async Task ApplyReservationStateAsync(Listing listing, int requestedQuantity)
    {
        var reservedQuantity = await _dbContext.Orders
            .Where(order =>
                order.ListingId == listing.Id &&
                order.Status != OrderStatus.Delivered &&
                order.Status != OrderStatus.Declined &&
                order.Status != OrderStatus.Cancelled)
            .SumAsync(order => (int?)order.Quantity) ?? 0;

        var remainingQuantity = listing.Quantity - reservedQuantity - requestedQuantity;
        listing.Status = remainingQuantity <= 0 ? ListingStatus.ReadyToShip : ListingStatus.Published;
    }

    private async Task RecalculateListingStatusAsync(Listing listing)
    {
        if (listing.Status == ListingStatus.Blocked || listing.Status == ListingStatus.Archived)
        {
            return;
        }

        if (listing.Quantity <= 0)
        {
            listing.Status = ListingStatus.Inactive;
            return;
        }

        var activeOrders = await _dbContext.Orders
            .Where(order =>
                order.ListingId == listing.Id &&
                order.Status != OrderStatus.Delivered &&
                order.Status != OrderStatus.Declined &&
                order.Status != OrderStatus.Cancelled)
            .ToListAsync();

        if (activeOrders.Count == 0)
        {
            listing.Status = ListingStatus.Published;
            return;
        }

        var reservedQuantity = activeOrders.Sum(order => order.Quantity);
        if (listing.Quantity > reservedQuantity)
        {
            listing.Status = ListingStatus.Published;
            return;
        }

        if (activeOrders.Any(order => order.Status == OrderStatus.Shipped))
        {
            listing.Status = ListingStatus.InDelivery;
            return;
        }

        listing.Status = ListingStatus.ReadyToShip;
    }

    private static void ValidatePaymentDetails(PaymentMethod paymentMethod, string? cardNumber, string? cardExpiry, string? cardCvc)
    {
        if (paymentMethod != PaymentMethod.Card)
        {
            return;
        }

        var normalizedCardNumber = NormalizeDigits(cardNumber);
        var normalizedCvc = NormalizeDigits(cardCvc);
        var normalizedExpiry = cardExpiry?.Trim() ?? string.Empty;

        if (normalizedCardNumber.Length < 12 || normalizedCardNumber.Length > 19)
        {
            throw new InvalidOperationException("Enter a valid card number.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedExpiry, "^(0[1-9]|1[0-2])/[0-9]{2}$"))
        {
            throw new InvalidOperationException("Enter card expiry in MM/YY format.");
        }

        if (normalizedCvc.Length is < 3 or > 4)
        {
            throw new InvalidOperationException("Enter a valid CVC.");
        }
    }

    private static string? MaskCardNumber(string? cardNumber)
    {
        var digits = NormalizeDigits(cardNumber);
        if (digits.Length < 4)
        {
            return null;
        }

        return $"**** **** **** {digits[^4..]}";
    }

    private static string NormalizeDigits(string? value)
    {
        return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
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
}
