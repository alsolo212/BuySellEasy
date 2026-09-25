using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Cart;

public class AddCartItemRequest
{
    [Required]
    public Guid ListingId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; } = 1;
}
