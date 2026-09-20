namespace PiAiAssistant.Infrastructure.Options;

/// <summary>
/// Tracks which PI data strategy is active. Mutable so Development can switch
/// from demo-fallback → simulator when the emulator comes online.
/// </summary>
public sealed class PiDataSourceRuntimeInfo
{
    private readonly object _gate = new();

    public PiDataSourceRuntimeInfo(
        bool useDemo,
        bool developmentPreferSimulator,
        bool fellBackFromSimulator,
        string configuredBaseUrl,
        string activeSource)
    {
        UseDemo = useDemo;
        DevelopmentPreferSimulator = developmentPreferSimulator;
        FellBackFromSimulator = fellBackFromSimulator;
        ConfiguredBaseUrl = configuredBaseUrl;
        ActiveSource = activeSource;
    }

    public bool UseDemo { get; private set; }
    public bool DevelopmentPreferSimulator { get; }
    public bool FellBackFromSimulator { get; private set; }
    public string ConfiguredBaseUrl { get; }
    public string ActiveSource { get; private set; }

    public void SetSimulator()
    {
        lock (_gate)
        {
            UseDemo = false;
            FellBackFromSimulator = false;
            ActiveSource = DevelopmentPreferSimulator ? "simulator" : "live";
        }
    }

    public void SetDemoFallback()
    {
        lock (_gate)
        {
            UseDemo = true;
            FellBackFromSimulator = DevelopmentPreferSimulator;
            ActiveSource = DevelopmentPreferSimulator ? "demo-fallback" : "demo";
        }
    }

    public void SetDemo()
    {
        lock (_gate)
        {
            UseDemo = true;
            FellBackFromSimulator = false;
            ActiveSource = "demo";
        }
    }

    public static PiDataSourceRuntimeInfo Demo(string baseUrl, bool developmentPreferSimulator, bool fellBack) =>
        new(true, developmentPreferSimulator, fellBack, baseUrl, fellBack ? "demo-fallback" : "demo");

    public static PiDataSourceRuntimeInfo Simulator(string baseUrl) =>
        new(false, true, false, baseUrl, "simulator");

    public static PiDataSourceRuntimeInfo Live(string baseUrl) =>
        new(false, false, false, baseUrl, "live");
}
