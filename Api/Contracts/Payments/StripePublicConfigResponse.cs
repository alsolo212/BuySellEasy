namespace Api.Contracts.Payments;

public class StripePublicConfigResponse
{
    public required string PublishableKey { get; init; }
}
