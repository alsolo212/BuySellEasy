namespace Api.Contracts.Chats;

public class ChatSummaryResponse
{
    public required Guid Id { get; init; }

    public Guid? ListingId { get; init; }

    public string? ListingTitle { get; init; }

    public string? ListingPrimaryImageUrl { get; init; }

    public required Guid CounterpartyId { get; init; }

    public required string CounterpartyName { get; init; }

    public string? CounterpartyAvatarUrl { get; init; }

    public bool IsSellerView { get; init; }

    public bool IsSupport { get; init; }

    public Guid? AssignedAdminId { get; init; }

    public string? AssignedAdminName { get; init; }

    public bool IsBusy { get; init; }

    public bool IsAssignedToCurrentAdmin { get; init; }

    public int UnreadCount { get; init; }

    public string LastMessagePreview { get; init; } = string.Empty;

    public DateTime LastMessageAtUtc { get; init; }
}
