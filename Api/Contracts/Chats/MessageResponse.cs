using Domain.Enums;

namespace Api.Contracts.Chats;

public class MessageResponse
{
    public required Guid Id { get; init; }

    public required Guid ChatId { get; init; }

    public required Guid SenderId { get; init; }

    public required string SenderName { get; init; }

    public required string Content { get; init; }

    public MessageType Type { get; init; }

    public string? AttachmentUrl { get; init; }

    public DateTime SentAtUtc { get; init; }

    public DateTime? ReadAtUtc { get; init; }
}
