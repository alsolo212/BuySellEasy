using Api.Contracts.Cart;
using Api.Contracts.Catalog;
using Api.Contracts.Chats;
using Api.Contracts.Admin;
using Api.Contracts.Listings;
using Api.Contracts.Orders;
using Api.Contracts.Reviews;
using Api.Contracts.Users;
using Domain.Entities;
using Domain.Enums;
using Domain.IdentityEntities;

namespace Api.Mappers;

public static class MarketplaceMapper
{
    public static CategoryListItemResponse MapCategory(Category category) => new()
    {
        Id = category.Id,
        Name = category.Name ?? string.Empty,
        ImageUrl = category.ImageUrl
    };

    public static ListingOwnerResponse MapListingOwner(User user) => new()
    {
        Id = user.Id,
        UserName = user.UserName ?? string.Empty,
        ProfileImageUrl = user.ProfileImageUrl,
        IsVerified = user.IsVerified
    };

    public static UserProfileResponse MapUserProfile(User user) => new()
    {
        Id = user.Id,
        UserName = user.UserName ?? string.Empty,
        Email = user.Email ?? string.Empty,
        Phone = user.PhoneNumber,
        ProfileImageUrl = user.ProfileImageUrl,
        CreatedAtUtc = user.CreatedAt,
        IsVerified = user.IsVerified,
        IsBlocked = user.IsBlocked,
        ActiveListingsCount = user.ActiveListingsCount,
        AverageRating = user.ReceivedReviews.Any(review =>
            review.Status != ReviewStatus.Removed &&
            review.Status != ReviewStatus.Hidden)
            ? Math.Round(user.ReceivedReviews
                .Where(review =>
                    review.Status != ReviewStatus.Removed &&
                    review.Status != ReviewStatus.Hidden)
                .Average(review => review.Rating), 2)
            : 0,
        ReviewsCount = user.ReceivedReviews.Count(review =>
            review.Status != ReviewStatus.Removed &&
            review.Status != ReviewStatus.Hidden)
    };

    public static ListingSummaryResponse MapListingSummary(Listing listing) => new()
    {
        Id = listing.Id,
        Title = listing.Title,
        Price = listing.Price,
        Quantity = listing.Quantity,
        IsNegotiable = listing.IsNegotiable,
        City = listing.City,
        Condition = listing.Condition,
        Status = listing.Status,
        HasActiveOrders = listing.Orders.Any(order =>
            order.Status != OrderStatus.Declined &&
            order.Status != OrderStatus.Cancelled &&
            order.Status != OrderStatus.Delivered),
        CategoryId = listing.CategoryId,
        CategoryName = listing.Category.Name ?? string.Empty,
        PrimaryImageUrl = listing.Images.OrderBy(image => image.SortOrder).Select(image => image.Url).FirstOrDefault(),
        CreatedAtUtc = listing.CreatedAtUtc,
        Owner = MapListingOwner(listing.Owner)
    };

    public static ListingDetailsResponse MapListingDetails(Listing listing) => new()
    {
        Id = listing.Id,
        Title = listing.Title,
        Description = listing.Description,
        Price = listing.Price,
        Quantity = listing.Quantity,
        IsNegotiable = listing.IsNegotiable,
        City = listing.City,
        Condition = listing.Condition,
        Status = listing.Status,
        HasActiveOrders = listing.Orders.Any(order =>
            order.Status != OrderStatus.Declined &&
            order.Status != OrderStatus.Cancelled &&
            order.Status != OrderStatus.Delivered),
        CategoryId = listing.CategoryId,
        CategoryName = listing.Category.Name ?? string.Empty,
        CreatedAtUtc = listing.CreatedAtUtc,
        UpdatedAtUtc = listing.UpdatedAtUtc,
        Owner = MapListingOwner(listing.Owner),
        Images = listing.Images
            .OrderBy(image => image.SortOrder)
            .Select(image => new ListingImageResponse
            {
                Id = image.Id,
                Url = image.Url,
                SortOrder = image.SortOrder
            })
            .ToArray()
    };

    public static ShipmentResponse MapShipment(Shipment shipment) => new()
    {
        Id = shipment.Id,
        Status = shipment.Status,
        RecipientFirstName = shipment.RecipientFirstName,
        RecipientLastName = shipment.RecipientLastName,
        RecipientPhone = shipment.RecipientPhone,
        DeliveryAddress = shipment.DeliveryAddress,
        DeliveryCity = shipment.DeliveryCity,
        SenderFirstName = shipment.SenderFirstName,
        SenderLastName = shipment.SenderLastName,
        SenderPhone = shipment.SenderPhone,
        PayoutCardMasked = shipment.PayoutCardMasked,
        ShippedAtUtc = shipment.ShippedAtUtc,
        ArrivedAtUtc = shipment.ArrivedAtUtc
    };

    public static CartItemResponse MapCartItem(CartItem cartItem) => new()
    {
        Id = cartItem.Id,
        IsSelected = cartItem.IsSelected,
        Quantity = cartItem.Quantity,
        CreatedAtUtc = cartItem.CreatedAtUtc,
        Listing = new CartItemListingResponse
        {
            Id = cartItem.Listing!.Id,
            Title = cartItem.Listing.Title,
            Price = cartItem.Listing.Price,
            AvailableQuantity = cartItem.Listing.Quantity,
            IsNegotiable = cartItem.Listing.IsNegotiable,
            City = cartItem.Listing.City,
            CategoryName = cartItem.Listing.Category.Name ?? string.Empty,
            PrimaryImageUrl = cartItem.Listing.Images
                .OrderBy(image => image.SortOrder)
                .Select(image => image.Url)
                .FirstOrDefault(),
            SellerName = cartItem.Listing.Owner.UserName ?? string.Empty
        }
    };

    public static OrderSummaryResponse MapOrderSummary(Order order, Guid currentUserId) => new()
    {
        Id = order.Id,
        ListingId = order.ListingId,
        ListingTitle = string.IsNullOrWhiteSpace(order.ListingTitleSnapshot) ? order.Listing.Title : order.ListingTitleSnapshot,
        ListingPrimaryImageUrl = string.IsNullOrWhiteSpace(order.ListingPrimaryImageUrlSnapshot)
            ? order.Listing.Images.OrderBy(image => image.SortOrder).Select(image => image.Url).FirstOrDefault()
            : order.ListingPrimaryImageUrlSnapshot,
        BuyerId = order.BuyerId,
        SellerId = order.SellerId,
        CounterpartyName = order.BuyerId == currentUserId
            ? order.Seller.UserName ?? string.Empty
            : order.Buyer.UserName ?? string.Empty,
        Status = order.Status,
        PaymentMethod = order.PaymentMethod,
        Quantity = order.Quantity,
        PaymentCardMasked = order.PaymentCardMasked,
        TotalAmount = order.TotalAmount,
        DeliveryCity = order.DeliveryCity,
        DeliveryAddress = order.DeliveryAddress,
        CreatedAtUtc = order.CreatedAtUtc,
        ShipmentStatus = order.Shipment?.Status,
        HasReviewFromCurrentUser = order.Reviews.Any(review => review.AuthorId == currentUserId)
    };

    public static OrderDetailsResponse MapOrderDetails(Order order) => new()
    {
        Id = order.Id,
        Status = order.Status,
        PaymentMethod = order.PaymentMethod,
        Quantity = order.Quantity,
        PaymentCardMasked = order.PaymentCardMasked,
        TotalAmount = order.TotalAmount,
        BuyerFirstName = order.BuyerFirstName,
        BuyerLastName = order.BuyerLastName,
        BuyerEmail = order.BuyerEmail,
        BuyerPhone = order.BuyerPhone,
        DeliveryAddress = order.DeliveryAddress,
        DeliveryCity = order.DeliveryCity,
        CreatedAtUtc = order.CreatedAtUtc,
        UpdatedAtUtc = order.UpdatedAtUtc,
        Listing = MapListingSummary(order.Listing),
        Buyer = MapUserProfile(order.Buyer),
        Seller = MapUserProfile(order.Seller),
        Shipment = order.Shipment is null ? null : MapShipment(order.Shipment)
    };

    public static ChatSummaryResponse MapChatSummary(Chat chat, Guid currentUserId)
    {
        var currentUserIsElevatedAdmin = chat.BuyerId != currentUserId && chat.SellerId != currentUserId;
        var currentUserIsSupportAgent = chat.IsSupport && chat.BuyerId != currentUserId;
        var counterparty = currentUserIsElevatedAdmin
            ? chat.Buyer
            : chat.BuyerId == currentUserId
                ? chat.Seller
                : chat.Buyer;
        var lastMessage = chat.Messages
            .OrderByDescending(message => message.SentAtUtc)
            .FirstOrDefault();
        var unreadCount = chat.Messages.Count(message =>
            message.SenderId != currentUserId &&
            message.ReadAtUtc is null);
        var lastMessagePreview = string.IsNullOrWhiteSpace(lastMessage?.Content)
            ? lastMessage?.Type switch
            {
                MessageType.Image => "Image",
                MessageType.File => "File",
                _ => string.Empty
            }
            : lastMessage!.Content;

        return new ChatSummaryResponse
        {
            Id = chat.Id,
            ListingId = chat.ListingId,
            ListingTitle = chat.IsSupport ? "Support" : chat.Listing?.Title,
            ListingPrimaryImageUrl = chat.Listing?.Images
                .OrderBy(image => image.SortOrder)
                .Select(image => image.Url)
                .FirstOrDefault(),
            CounterpartyId = chat.IsSupport && !currentUserIsElevatedAdmin && chat.BuyerId == currentUserId
                ? chat.SellerId
                : counterparty.Id,
            CounterpartyName = chat.IsSupport
                ? currentUserIsSupportAgent || currentUserIsElevatedAdmin
                    ? chat.Buyer.UserName ?? string.Empty
                    : chat.AssignedAdmin?.UserName ?? chat.Seller.UserName ?? "Support"
                : counterparty.UserName ?? string.Empty,
            CounterpartyAvatarUrl = chat.IsSupport && !currentUserIsElevatedAdmin && chat.BuyerId == currentUserId
                ? chat.AssignedAdmin?.ProfileImageUrl ?? chat.Seller.ProfileImageUrl
                : counterparty.ProfileImageUrl,
            IsSellerView = chat.SellerId == currentUserId,
            IsSupport = chat.IsSupport,
            AssignedAdminId = chat.AssignedAdminId,
            AssignedAdminName = chat.AssignedAdmin?.UserName,
            IsBusy = chat.IsSupport && chat.AssignedAdminId.HasValue,
            IsAssignedToCurrentAdmin = chat.IsSupport && chat.AssignedAdminId == currentUserId,
            UnreadCount = unreadCount,
            LastMessagePreview = lastMessagePreview,
            LastMessageAtUtc = chat.LastMessageAtUtc
        };
    }

    public static MessageResponse MapMessage(Message message) => new()
    {
        Id = message.Id,
        ChatId = message.ChatId,
        SenderId = message.SenderId,
        SenderName = message.Sender.UserName ?? string.Empty,
        Content = message.Content,
        Type = message.Type,
        AttachmentUrl = message.AttachmentUrl,
        SentAtUtc = message.SentAtUtc,
        ReadAtUtc = message.ReadAtUtc
    };

    public static ReviewResponse MapReview(Review review) => new()
    {
        Id = review.Id,
        OrderId = review.OrderId,
        ListingId = review.ListingId,
        ListingTitle = review.Listing?.Title,
        AuthorId = review.AuthorId,
        AuthorName = review.Author.UserName ?? string.Empty,
        TargetUserId = review.TargetUserId,
        TargetUserName = review.TargetUser.UserName ?? string.Empty,
        Rating = review.Rating,
        Comment = review.Comment,
        Status = review.Status,
        DisputeReason = review.DisputeReason,
        OrderStatus = review.Order?.Status.ToString(),
        CreatedAtUtc = review.CreatedAtUtc
    };

    public static ListingReportResponse MapListingReport(ListingReport report) => new()
    {
        Id = report.Id,
        ListingId = report.ListingId,
        ListingTitle = report.Listing.Title,
        ListingPrimaryImageUrl = report.Listing.Images
            .OrderBy(image => image.SortOrder)
            .Select(image => image.Url)
            .FirstOrDefault(),
        OwnerId = report.Listing.OwnerId,
        OwnerName = report.Listing.Owner.UserName ?? string.Empty,
        ReporterId = report.ReporterId,
        ReporterName = report.Reporter.UserName ?? string.Empty,
        Reason = report.Reason,
        Status = report.Status,
        CreatedAtUtc = report.CreatedAtUtc,
        ResolvedAtUtc = report.ResolvedAtUtc
    };
}
