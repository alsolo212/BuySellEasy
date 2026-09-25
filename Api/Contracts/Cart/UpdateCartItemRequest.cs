using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Cart;

public class UpdateCartItemRequest
{
    [Required]
    public bool IsSelected { get; init; }

    [Range(1, int.MaxValue)]
    public int? Quantity { get; init; }
}
