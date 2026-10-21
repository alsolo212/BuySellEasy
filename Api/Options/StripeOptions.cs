namespace Api.Options;

public class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;

    public string PublishableKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public string FrontendBaseUrl { get; set; } = string.Empty;

    public string ConnectCountry { get; set; } = "US";
}
