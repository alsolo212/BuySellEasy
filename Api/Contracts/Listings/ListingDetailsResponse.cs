using Domain.Enums;

namespace Api.Contracts.Listings;

public class ListingDetailsResponse
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public decimal Price { get; init; }

    public int Quantity { get; init; }

    public bool IsNegotiable { get; init; }

    public required string City { get; init; }

    public ListingCondition Condition { get; init; }

    public ListingStatus Status { get; init; }

    public bool HasActiveOrders { get; init; }

    public Guid CategoryId { get; init; }

    public required string CategoryName { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }

    public required ListingOwnerResponse Owner { get; init; }

    public required IReadOnlyCollection<ListingImageResponse> Images { get; init; }
}
