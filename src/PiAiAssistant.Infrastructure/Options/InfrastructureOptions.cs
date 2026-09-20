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

    /// <summary>Default local model used when the request does not specify one.</summary>
    public string DefaultModel { get; set; } = "qwen3:8b";

    /// <summary>Backward-compatible alias for <see cref="DefaultModel"/> (appsettings "Model").</summary>
    public string Model
    {
        get => DefaultModel;
        set => DefaultModel = value;
    }

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Selectable local Ollama models. Keys are API/UI ids; values are Ollama model names.
    /// Example: qwen → qwen3:8b, deepseek → deepseek-r1:1.5b
    /// </summary>
    public Dictionary<string, string> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["qwen"] = "qwen3:8b",
        ["deepseek"] = "deepseek-r1:1.5b"
    };

    public IReadOnlyList<LocalModelInfo> ListModels()
    {
        EnsureModelsSeeded();
        return Models
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new LocalModelInfo(
                kv.Key,
                kv.Value,
                string.Equals(kv.Value, DefaultModel, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public string ResolveModelId(string? requested)
    {
        EnsureModelsSeeded();
        if (string.IsNullOrWhiteSpace(requested))
        {
            return DefaultModel;
        }

        var key = requested.Trim();
        if (Models.TryGetValue(key, out var byKey) && !string.IsNullOrWhiteSpace(byKey))
        {
            return byKey;
        }

        var byValue = Models.Values.FirstOrDefault(v =>
            string.Equals(v, key, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(byValue))
        {
            return byValue;
        }

        // Unknown model — fall back to default (do not send arbitrary remote model ids).
        return DefaultModel;
    }

    private void EnsureModelsSeeded()
    {
        Models ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Models.Count == 0)
        {
            Models["qwen"] = DefaultModel;
            Models["deepseek"] = "deepseek-r1:1.5b";
        }

        if (!Models.Values.Any(v => string.Equals(v, DefaultModel, StringComparison.OrdinalIgnoreCase)))
        {
            Models["default"] = DefaultModel;
        }
    }
}

public sealed record LocalModelInfo(string Key, string ModelId, bool IsDefault);

public sealed class QwenOnlineOptions
{
    public const string SectionName = "QwenOnline";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "qwen-max";
    public bool Enabled { get; set; } = false;
}
