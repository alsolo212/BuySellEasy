namespace Api.Contracts.Payments;

public class CreatePaymentIntentResponse
{
    public required string PaymentIntentId { get; init; }

    public required string ClientSecret { get; init; }

    public required string PublishableKey { get; init; }
}
