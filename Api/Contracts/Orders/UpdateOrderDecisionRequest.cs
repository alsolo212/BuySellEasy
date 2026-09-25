using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Orders;

public class UpdateOrderDecisionRequest
{
    [Required]
    public bool Approve { get; init; }
}
