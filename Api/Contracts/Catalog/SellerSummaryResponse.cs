namespace Api.Contracts.Catalog;

public class SellerSummaryResponse
{
    public required Guid Id { get; init; }

    public required string UserName { get; init; }

    public bool IsVerified { get; init; }

    public string? ProfileImageUrl { get; init; }
}
