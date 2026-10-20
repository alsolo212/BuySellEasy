using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.DbContextt;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using BsePaymentMethod = Domain.Enums.PaymentMethod;

namespace Api.Services;

public sealed class OrderPaymentLifecycleService
{
    private static readonly TimeSpan SellerDecisionWindow = TimeSpan.FromDays(4);
    private static readonly TimeSpan PickupConfirmationWindow = TimeSpan.FromDays(7);
    private readonly ProductDbContext _dbContext;
    private readonly InventoryService _inventory;
    private readonly OrderRealtimeNotifier _orderRealtime;
    private readonly StripePaymentService _stripe;
    private readonly ILogger<OrderPaymentLifecycleService> _logger;

    public OrderPaymentLifecycleService(
        ProductDbContext dbContext,
        InventoryService inventory,
        OrderRealtimeNotifier orderRealtime,
        StripePaymentService stripe,
        ILogger<OrderPaymentLifecycleService> logger)
    {
        _dbContext = dbContext;
        _inventory = inventory;
        _orderRealtime = orderRealtime;
        _stripe = stripe;
        _logger = logger;
    }

    public static DateTime GetSellerDecisionDeadline(DateTime createdAtUtc)
    {
        return createdAtUtc.Add(SellerDecisionWindow);
    }

    public static DateTime GetAutomaticDeliveryDeadline(DateTime readyForPickupAtUtc)
    {
        return readyForPickupAtUtc.Add(PickupConfirmationWindow);
    }

    public async Task ProcessCheckoutCompletedAsync(
        Session session,
        CancellationToken cancellationToken = default)
    {
        var paymentSessionId = session.Metadata.TryGetValue("payment_session_id", out var metadataId)
            ? metadataId
            : session.ClientReferenceId;

        if (!Guid.TryParse(paymentSessionId, out var parsedPaymentSessionId))
        {
            throw new InvalidOperationException("Stripe Checkout session has no valid BSE payment session id.");
        }

        if (string.IsNullOrWhiteSpace(session.PaymentIntentId))
        {
            throw new InvalidOperationException("Stripe Checkout session has no payment intent.");
        }

        await ProcessPaidPaymentAsync(
            parsedPaymentSessionId,
            session.PaymentIntentId,
            session.Id,
            cancellationToken);
    }

    public async Task ProcessPaymentIntentSucceededAsync(
        PaymentIntent paymentIntent,
        CancellationToken cancellationToken = default)
    {
        var paymentSessionId = paymentIntent.Metadata.TryGetValue("payment_session_id", out var metadataId)
            ? metadataId
            : string.Empty;

        if (!Guid.TryParse(paymentSessionId, out var parsedPaymentSessionId))
        {
            throw new InvalidOperationException("PaymentIntent has no valid BSE payment session id.");
        }

        await ProcessPaidPaymentAsync(
            parsedPaymentSessionId,
            paymentIntent.Id,
            null,
            cancellationToken);
    }

    private async Task ProcessPaidPaymentAsync(
        Guid paymentSessionId,
        string paymentIntentId,
        string? checkoutSessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var paymentSession = await LoadPaymentSessionForUpdateAsync(paymentSessionId, cancellationToken)
                ?? throw new KeyNotFoundException("The BSE payment session was not found.");

            if (paymentSession.Status == PaymentSessionStatus.Processed)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            if (paymentSession.Status == PaymentSessionStatus.Failed &&
                string.Equals(paymentSession.StripePaymentIntentId, paymentIntentId, StringComparison.Ordinal))
            {
                await transaction.CommitAsync(cancellationToken);
                await RefundRejectedPaymentAsync(paymentSessionId, paymentIntentId, cancellationToken);
                return;
            }

            if (!string.IsNullOrWhiteSpace(paymentSession.StripePaymentIntentId) &&
                !string.Equals(paymentSession.StripePaymentIntentId, paymentIntentId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The payment intent does not match the BSE payment session.");
            }

            if (!string.IsNullOrWhiteSpace(checkoutSessionId) &&
                !paymentSession.StripeCheckoutSessionId.StartsWith("pending-", StringComparison.Ordinal) &&
                !paymentSession.StripeCheckoutSessionId.StartsWith("payment-intent-pending-", StringComparison.Ordinal) &&
                !string.Equals(paymentSession.StripeCheckoutSessionId, checkoutSessionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The Checkout session does not match the BSE payment session.");
            }

            var draft = JsonSerializer.Deserialize<CheckoutPaymentDraft>(paymentSession.DraftJson)
                ?? throw new InvalidOperationException("The BSE payment draft is invalid.");

            if (draft.BuyerId != paymentSession.BuyerId || draft.Items.Count == 0)
            {
                throw new PaymentFinalizationException("The BSE payment draft is no longer valid.");
            }

            var listingIds = draft.Items.Select(item => item.ListingId).Distinct().ToArray();
            var listings = await _dbContext.Listings
                .AsNoTracking()
                .Include(item => item.Owner)
                .Include(item => item.Images)
                .Where(item => listingIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

            var validationError = ValidateCurrentPaymentState(paymentSession, draft, listings);
            if (validationError is not null)
            {
                throw new PaymentFinalizationException(validationError);
            }

            if (draft.SaveCard)
            {
                await _stripe.KeepOnlyLatestSavedCardAsync(paymentSession.Buyer, paymentIntentId, cancellationToken);
            }

            var feeInCents = await _stripe.GetPaymentFeeInCentsAsync(paymentIntentId, cancellationToken);
            var grossAmountInCents = draft.Items.Sum(item => StripePaymentService.ToStripeAmount(item.UnitPrice * item.Quantity));
            var now = DateTime.UtcNow;

            foreach (var item in draft.Items.OrderBy(item => item.ListingId))
            {
                if (!await _inventory.TryReserveAsync(item.ListingId, item.Quantity, item.UnitPrice, cancellationToken))
                {
                    throw new PaymentFinalizationException("The requested inventory changed while payment was completing.");
                }
            }

            var createdOrders = new List<Order>();
            foreach (var item in draft.Items)
            {
                var listing = listings[item.ListingId];
                var grossItemAmountInCents = StripePaymentService.ToStripeAmount(item.UnitPrice * item.Quantity);
                var allocatedFeeInCents = grossAmountInCents == 0
                    ? 0
                    : (long)Math.Round(feeInCents * (decimal)grossItemAmountInCents / grossAmountInCents, MidpointRounding.AwayFromZero);

                createdOrders.Add(new Order
                {
                    ListingId = listing.Id,
                    BuyerId = draft.BuyerId,
                    SellerId = listing.OwnerId,
                    Status = OrderStatus.Pending,
                    PaymentMethod = BsePaymentMethod.Card,
                    PaymentState = PaymentState.Paid,
                    InventoryReserved = true,
                    Quantity = item.Quantity,
                    TotalAmount = item.UnitPrice * item.Quantity,
                    StripeFeeAmount = allocatedFeeInCents / 100m,
                    StripeFeeCalculated = feeInCents > 0,
                    StripeCheckoutSessionId = checkoutSessionId,
                    StripePaymentIntentId = paymentIntentId,
                    StripeConnectAccountId = listing.Owner.StripeConnectAccountId,
                    ListingTitleSnapshot = listing.Title,
                    ListingPrimaryImageUrlSnapshot = listing.Images
                        .OrderBy(image => image.SortOrder)
                        .Select(image => image.Url)
                        .FirstOrDefault(),
                    BuyerFirstName = draft.BuyerFirstName,
                    BuyerLastName = draft.BuyerLastName,
                    BuyerEmail = draft.BuyerEmail,
                    BuyerPhone = draft.BuyerPhone,
                    DeliveryAddress = draft.DeliveryAddress,
                    DeliveryCity = draft.DeliveryCity,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    SellerDecisionDeadlineUtc = GetSellerDecisionDeadline(now),
                    Shipment = new Shipment
                    {
                        RecipientFirstName = draft.BuyerFirstName,
                        RecipientLastName = draft.BuyerLastName,
                        RecipientPhone = draft.BuyerPhone,
                        DeliveryAddress = draft.DeliveryAddress,
                        DeliveryCity = draft.DeliveryCity,
                        Status = ShipmentStatus.Pending,
                        SenderFirstName = listing.Owner.UserName ?? string.Empty,
                        SenderPhone = listing.Owner.PhoneNumber ?? string.Empty,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    }
                });
            }

            _dbContext.Orders.AddRange(createdOrders);
            foreach (var cartItemId in draft.CartItemIds)
            {
                var cartItem = await _dbContext.CartItems.FirstOrDefaultAsync(
                    item => item.Id == cartItemId && item.UserId == draft.BuyerId,
                    cancellationToken);
                if (cartItem is not null)
                {
                    _dbContext.CartItems.Remove(cartItem);
                }
            }

            paymentSession.StripePaymentIntentId = paymentIntentId;
            paymentSession.Status = PaymentSessionStatus.Processed;
            paymentSession.ProcessedAtUtc = now;
            paymentSession.UpdatedAtUtc = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            foreach (var createdOrder in createdOrders)
            {
                await _orderRealtime.NotifyAsync(createdOrder, "created", cancellationToken);
            }
        }
        catch (PaymentFinalizationException exception)
        {
            await FailPaymentAndRefundAsync(paymentSessionId, paymentIntentId, exception.Message, cancellationToken);
        }
    }

    private async Task<PaymentSession?> LoadPaymentSessionForUpdateAsync(
        Guid paymentSessionId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.PaymentSessions
            .FromSqlInterpolated($"SELECT * FROM \"PaymentSessions\" WHERE \"Id\" = {paymentSessionId} FOR UPDATE")
            .Include(item => item.Buyer)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static string? ValidateCurrentPaymentState(
        PaymentSession paymentSession,
        CheckoutPaymentDraft draft,
        IReadOnlyDictionary<Guid, Listing> listings)
    {
        var expectedTotal = draft.Items.Sum(item => item.UnitPrice * item.Quantity);
        if (paymentSession.TotalAmount != expectedTotal)
        {
            return "The payment total no longer matches the checkout draft.";
        }

        foreach (var item in draft.Items)
        {
            if (!listings.TryGetValue(item.ListingId, out var listing))
            {
                return "A listing from the paid cart no longer exists.";
            }

            if (listing.OwnerId != item.SellerId || item.Quantity < 1)
            {
                return "The paid cart contains an invalid listing item.";
            }

            if (listing.Price != item.UnitPrice)
            {
                return "The listing price changed before payment completion.";
            }

            if (listing.Status is not (ListingStatus.Active or ListingStatus.Published))
            {
                return "The listing is no longer available for purchase.";
            }

            if (InventoryService.GetAvailableQuantity(listing.Quantity, listing.ReservedQuantity) < item.Quantity)
            {
                return "The requested inventory is no longer available.";
            }
        }

        return null;
    }

    private async Task FailPaymentAndRefundAsync(
        Guid paymentSessionId,
        string paymentIntentId,
        string reason,
        CancellationToken cancellationToken)
    {
        var paymentSession = await _dbContext.PaymentSessions
            .FirstOrDefaultAsync(item => item.Id == paymentSessionId, cancellationToken)
            ?? throw new KeyNotFoundException("The BSE payment session was not found.");

        if (paymentSession.Status == PaymentSessionStatus.Processed)
        {
            return;
        }

        paymentSession.StripePaymentIntentId = paymentIntentId;
        paymentSession.Status = PaymentSessionStatus.Failed;
        paymentSession.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Payment session {PaymentSessionId} was rejected after payment completion: {Reason}",
            paymentSessionId,
            reason);

        await RefundRejectedPaymentAsync(paymentSessionId, paymentIntentId, cancellationToken);
    }

    private async Task RefundRejectedPaymentAsync(
        Guid paymentSessionId,
        string paymentIntentId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _stripe.RefundPaymentIntentAsync(paymentSessionId, paymentIntentId, cancellationToken);
        }
        catch (StripeException exception)
        {
            _logger.LogError(
                exception,
                "Unable to refund rejected payment session {PaymentSessionId}; Stripe webhook retry remains safe.",
                paymentSessionId);
            throw;
        }
    }

    private sealed class PaymentFinalizationException : InvalidOperationException
    {
        public PaymentFinalizationException(string message) : base(message) { }
    }

    public async Task MarkDeliveredAndTransferAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order.Status == OrderStatus.Delivered && order.PaymentState == PaymentState.Transferred)
        {
            return;
        }

        if (order.Status != OrderStatus.Delivered)
        {
            if (!await _inventory.TryCompleteSaleAsync(
                    order.ListingId,
                    order.Quantity,
                    order.InventoryReserved,
                    cancellationToken))
            {
                throw new InvalidOperationException("The listing inventory could not be finalized for this order.");
            }

            order.InventoryReserved = false;
            order.Status = OrderStatus.Delivered;
            order.DeliveredAtUtc = DateTime.UtcNow;
            order.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _orderRealtime.NotifyAsync(order, cancellationToken: cancellationToken);
        }

        if (order.PaymentMethod != BsePaymentMethod.Card || order.PaymentState == PaymentState.Transferred)
        {
            return;
        }

        if (order.PaymentState != PaymentState.TransferPending)
        {
            order.PaymentState = PaymentState.TransferPending;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _orderRealtime.NotifyAsync(order, cancellationToken: cancellationToken);
        }

        try
        {
            order.StripeConnectAccountId ??= order.Seller.StripeConnectAccountId;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await EnsureStripeFeeAllocatedAsync(order, cancellationToken);
            if (string.IsNullOrWhiteSpace(order.StripeConnectAccountId) ||
                !await _stripe.CanReceiveTransfersAsync(order.Seller, cancellationToken))
            {
                _logger.LogDebug("Seller {SellerId} is not ready to receive transfer for order {OrderId}; it will remain pending.", order.SellerId, order.Id);
                return;
            }

            var amountInCents = GetNetAmountInCents(order);
            if (amountInCents > 0)
            {
                order.StripeTransferId = await _stripe.CreateTransferAsync(
                    order,
                    order.StripeConnectAccountId!,
                    amountInCents,
                    cancellationToken);
            }

            order.PaymentState = PaymentState.Transferred;
            order.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _orderRealtime.NotifyAsync(order, cancellationToken: cancellationToken);
        }
        catch (StripeException exception) when (IsTerminalTransferFailure(exception))
        {
            order.PaymentState = PaymentState.Failed;
            order.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _orderRealtime.NotifyAsync(order, cancellationToken: cancellationToken);

            _logger.LogWarning(
                "Payout for order {OrderId} was not transferred and will not be retried because the Stripe source charge has no transferable amount. {StripeMessage}",
                order.Id,
                exception.StripeError?.Message ?? exception.Message);
        }
        catch (StripeException exception)
        {
            // Balance and settlement delays are expected to be recoverable. Keep the
            // order pending so the background worker can retry after Stripe funds settle,
            // but do not report a normal retry as an application error.
            _logger.LogDebug(
                exception,
                "Payout for order {OrderId} remains pending and will be retried when Stripe funds are available.",
                order.Id);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(exception, "Unable to transfer payout for order {OrderId}.", order.Id);
        }
    }

    private static bool IsTerminalTransferFailure(StripeException exception)
    {
        var code = exception.StripeError?.Code;
        var message = exception.StripeError?.Message ?? exception.Message;

        return string.Equals(code, "amount_too_large", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "charge_already_refunded", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("must not exceed the source amount", StringComparison.OrdinalIgnoreCase);
    }

    public async Task RefundAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order.PaymentState is PaymentState.Refunded or PaymentState.NotApplicable)
        {
            await ReleaseInventoryReservationAsync(order, cancellationToken);
            return;
        }

        if (order.PaymentMethod != BsePaymentMethod.Card)
        {
            return;
        }

        if (order.PaymentState == PaymentState.RefundPending)
        {
            return;
        }

        await ReleaseInventoryReservationAsync(order, cancellationToken);

        order.PaymentState = PaymentState.RefundPending;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _orderRealtime.NotifyAsync(order, cancellationToken: cancellationToken);

        try
        {
            await EnsureStripeFeeAllocatedAsync(order, cancellationToken);
            var amountInCents = GetNetAmountInCents(order);
            if (amountInCents > 0)
            {
                order.StripeRefundId = await _stripe.CreateRefundAsync(order, amountInCents, cancellationToken);
            }

            order.PaymentState = PaymentState.Refunded;
            order.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _orderRealtime.NotifyAsync(order, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is StripeException or InvalidOperationException)
        {
            _logger.LogError(exception, "Unable to refund order {OrderId}; it will be retried.", order.Id);
        }
    }

    public async Task ProcessDueOrdersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var pickupDeadlineCutoff = now.Subtract(PickupConfirmationWindow);
        var expiredOrders = await _dbContext.Orders
            .Include(order => order.Listing)
            .Include(order => order.Seller)
            .Include(order => order.Shipment)
            .Where(order =>
                order.Status == OrderStatus.Pending &&
                order.SellerDecisionDeadlineUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (var order in expiredOrders)
        {
            order.Status = OrderStatus.Expired;
            order.UpdatedAtUtc = now;
            if (order.Shipment is not null)
            {
                order.Shipment.Status = ShipmentStatus.Cancelled;
                order.Shipment.UpdatedAtUtc = now;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _orderRealtime.NotifyAsync(order, cancellationToken: cancellationToken);
            await RefundAsync(order, cancellationToken);
        }

        var readyOrders = await _dbContext.Orders
            .Include(order => order.Listing)
            .Include(order => order.Seller)
            .Include(order => order.Shipment)
            .Where(order =>
                order.Status == OrderStatus.ReadyForPickup &&
                order.ReadyForPickupAtUtc.HasValue &&
                order.ReadyForPickupAtUtc.Value <= pickupDeadlineCutoff)
            .ToListAsync(cancellationToken);

        foreach (var order in readyOrders)
        {
            await MarkDeliveredAndTransferAsync(order, cancellationToken);
        }

        var transferRetries = await _dbContext.Orders
            .Include(order => order.Listing)
            .Include(order => order.Seller)
            .Where(order =>
                order.Status == OrderStatus.Delivered &&
                order.PaymentState == PaymentState.TransferPending)
            .ToListAsync(cancellationToken);

        foreach (var order in transferRetries)
        {
            await MarkDeliveredAndTransferAsync(order, cancellationToken);
        }

        var refundRetries = await _dbContext.Orders
            .Include(order => order.Listing)
            .Where(order =>
                order.PaymentState == PaymentState.RefundPending &&
                (order.Status == OrderStatus.Declined ||
                 order.Status == OrderStatus.Cancelled ||
                 order.Status == OrderStatus.Expired))
            .ToListAsync(cancellationToken);

        foreach (var order in refundRetries)
        {
            await RefundAsync(order, cancellationToken);
        }
    }

    public static long GetNetAmountInCents(Order order)
    {
        return Math.Max(0, StripePaymentService.ToStripeAmount(order.TotalAmount - order.StripeFeeAmount));
    }

    private async Task EnsureStripeFeeAllocatedAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.PaymentMethod != BsePaymentMethod.Card || order.StripeFeeCalculated)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(order.StripePaymentIntentId))
        {
            return;
        }

        var feeInCents = await _stripe.GetPaymentFeeInCentsAsync(order.StripePaymentIntentId, cancellationToken);
        if (feeInCents <= 0)
        {
            return;
        }

        var siblingOrders = await _dbContext.Orders
            .Where(item => item.StripePaymentIntentId == order.StripePaymentIntentId)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var grossTotalInCents = siblingOrders.Sum(item => StripePaymentService.ToStripeAmount(item.TotalAmount));
        var allocatedSoFar = 0L;

        for (var index = 0; index < siblingOrders.Count; index++)
        {
            var sibling = siblingOrders[index];
            var grossItemInCents = StripePaymentService.ToStripeAmount(sibling.TotalAmount);
            var allocated = index == siblingOrders.Count - 1
                ? feeInCents - allocatedSoFar
                : (long)Math.Round(feeInCents * (decimal)grossItemInCents / grossTotalInCents, MidpointRounding.AwayFromZero);
            sibling.StripeFeeAmount = Math.Max(0, allocated) / 100m;
            sibling.StripeFeeCalculated = true;
            allocatedSoFar += allocated;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public static void ApplyDeliveredListingState(Listing listing, int soldQuantity)
    {
        if (listing.Quantity <= soldQuantity)
        {
            listing.Quantity = 0;
            listing.ReservedQuantity = 0;
            listing.Status = ListingStatus.Inactive;
            return;
        }

        listing.Quantity -= soldQuantity;
        listing.ReservedQuantity = Math.Max(0, listing.ReservedQuantity - soldQuantity);
        listing.Status = ListingStatus.Published;
    }

    private async Task ReleaseInventoryReservationAsync(Order order, CancellationToken cancellationToken)
    {
        if (!order.InventoryReserved)
        {
            return;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var markedReleased = await _dbContext.Orders
            .Where(item => item.Id == order.Id && item.InventoryReserved)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.InventoryReserved, false)
                .SetProperty(item => item.UpdatedAtUtc, DateTime.UtcNow), cancellationToken);

        if (markedReleased == 1 &&
            !await _inventory.TryReleaseAsync(order.ListingId, order.Quantity, cancellationToken))
        {
            throw new InvalidOperationException("The listing inventory reservation could not be released.");
        }

        await transaction.CommitAsync(cancellationToken);
        order.InventoryReserved = false;
    }
}

public sealed class OrderLifecycleBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderLifecycleBackgroundService> _logger;

    public OrderLifecycleBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderLifecycleBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var lifecycle = scope.ServiceProvider.GetRequiredService<OrderPaymentLifecycleService>();
                await lifecycle.ProcessDueOrdersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Order lifecycle processing failed.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
