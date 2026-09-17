namespace PiAiAssistant.Infrastructure.Options;

public sealed class PiConnectionOptions
{
    public const string SectionName = "PiConnection";

    public bool UseDemoMode { get; set; } = true;
    public string BaseUrl { get; set; } = "https://localhost/piwebapi";
    public string DataArchiveName { get; set; } = "PISERVER";
    public string DefaultAfServer { get; set; } = "AFSERVER";
    public string DefaultAfDatabase { get; set; } = "Production";

    /// <summary>Demo | Anonymous | Basic | Windows | DefaultCredentials | NetworkCredential | Bearer</summary>
    public string AuthMode { get; set; } = "Demo";

    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Domain { get; set; }
    public string? BearerToken { get; set; }
    public bool AcceptInvalidCertificates { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string[] SampleTagNames { get; set; } = [];
}

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";
    public string BaseUrl { get; set; } = "http://localhost:11434/";
    public string Model { get; set; } = "qwen3:8b";
    public bool Enabled { get; set; } = true;
}

public sealed class QwenOnlineOptions
{
    public const string SectionName = "QwenOnline";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "qwen-max";
    public bool Enabled { get; set; } = false;
}
