namespace AvevaPi.Client.Options;

/// <summary>
/// Connection settings for AVEVA PI Web API.
/// </summary>
public sealed class PiConnectionOptions
{
    public const string SectionName = "PiConnection";

    public bool UseDemoMode { get; set; } = true;
    public string BaseUrl { get; set; } = "https://localhost/piwebapi";
    public string DataArchiveName { get; set; } = "PISERVER";
    public string DefaultAfServer { get; set; } = "AFSERVER";
    public string DefaultAfDatabase { get; set; } = "Production";

    /// <summary>
    /// AuthMode: Demo | Anonymous | Basic | Windows | DefaultCredentials | NetworkCredential
    /// </summary>
    public string AuthMode { get; set; } = "Demo";

    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Domain { get; set; }
    public bool AcceptInvalidCertificates { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string[] SampleTagNames { get; set; } = [];
}
