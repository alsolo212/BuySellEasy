using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Orders;

public class CancelOrderRequest
{
    [StringLength(512)]
    public string Reason { get; init; } = string.Empty;
}
