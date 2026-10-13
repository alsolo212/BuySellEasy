namespace Domain.Enums;

/// <summary>
/// Internal payment lifecycle. This is intentionally not exposed as an order status.
/// </summary>
public enum PaymentState
{
    NotApplicable = 1,
    Paid = 2,
    TransferPending = 3,
    Transferred = 4,
    RefundPending = 5,
    Refunded = 6,
    Failed = 7
}
