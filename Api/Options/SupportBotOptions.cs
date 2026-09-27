namespace Api.Options;

public class SupportBotOptions
{
    public const string SectionName = "SupportBot";

    public bool Enabled { get; set; } = true;

    public string Endpoint { get; set; } = "http://localhost:11434/api/chat";

    public string Model { get; set; } = "qwen2.5:7b";

    public int TimeoutSeconds { get; set; } = 90;
}
