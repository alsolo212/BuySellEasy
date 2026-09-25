using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Chats;

public class SendMessageRequest
{
    [StringLength(4000)]
    public string Content { get; init; } = string.Empty;

    public MessageType Type { get; init; } = MessageType.Text;

    [StringLength(2048)]
    public string? AttachmentUrl { get; init; }
}
