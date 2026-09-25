using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Chats;

public class StartChatRequest
{
    [Required]
    public Guid ListingId { get; init; }
}
