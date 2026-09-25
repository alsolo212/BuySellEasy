using Domain.Enums;

namespace Api.Contracts.Chats;

public class ChatAttachmentUploadResponse
{
    public required string FileName { get; init; }

    public required string AttachmentUrl { get; init; }

    public required MessageType Type { get; init; }
}
