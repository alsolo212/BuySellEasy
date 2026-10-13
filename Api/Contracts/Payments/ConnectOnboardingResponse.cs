namespace Api.Contracts.Payments;

public class ConnectOnboardingResponse
{
    public required string ClientSecret { get; init; }

    public required string AccountId { get; init; }

    public required string PublishableKey { get; init; }

    public bool IsComplete { get; init; }
}
