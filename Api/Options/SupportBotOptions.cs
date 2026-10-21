namespace Api.Options;

public class SupportBotOptions
{
    public const string SectionName = "SupportBot";

    public bool Enabled { get; set; } = true;

    public string Endpoint { get; set; } = string.Empty;

    public string Model { get; set; } = "qwen2.5:7b";

    // These limits intentionally have conservative defaults. They protect the
    // local model process from a single chat consuming an unbounded context or
    // holding a worker connection for too long.
    public int TimeoutSeconds { get; set; } = 30;

    public int MaxHistoryMessages { get; set; } = 12;

    public int MaxContextCharacters { get; set; } = 6000;

    public int MaxContextTokens { get; set; } = 2048;

    public int MaxResponseCharacters { get; set; } = 1200;

    public int MaxResponseTokens { get; set; } = 384;

    public int RequestsPerMinute { get; set; } = 20;
}
