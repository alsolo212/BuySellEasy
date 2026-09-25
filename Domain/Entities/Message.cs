using Domain.Abstractions;
using Domain.Enums;
using Domain.IdentityEntities;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class Message : IHasId
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ChatId { get; set; }

        public Guid SenderId { get; set; }

        [StringLength(4000)]
        public string Content { get; set; } = string.Empty;

        public MessageType Type { get; set; } = MessageType.Text;

        [StringLength(2048)]
        public string? AttachmentUrl { get; set; }

        public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ReadAtUtc { get; set; }

        public Chat Chat { get; set; } = null!;

        public User Sender { get; set; } = null!;
    }
}
