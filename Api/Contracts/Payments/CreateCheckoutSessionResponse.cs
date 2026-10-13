namespace Api.Contracts.Payments;

public class CreateCheckoutSessionResponse
{
    public required string SessionId { get; init; }

    public required string Url { get; init; }
}
