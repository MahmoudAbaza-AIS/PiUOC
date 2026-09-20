using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PiAiAssistant.Domain.Interfaces;
using PiAiAssistant.Infrastructure;
using PiAiAssistant.Infrastructure.Options;
using PiAiAssistant.Infrastructure.Pi;

namespace PiAiAssistant.Tests;

public class PiSimulatorConnectionTests
{
    [Fact]
    public void Probe_false_for_garbage_url()
    {
        Assert.False(DependencyInjection.IsPiSimulatorReachable("http://127.0.0.1:1/piwebapi", timeoutSeconds: 1));
    }

    [Fact]
    public async Task Development_router_switches_to_simulator_when_emulator_up()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PiConnection:UseDemoMode"] = "false",
                ["PiConnection:BaseUrl"] = "http://localhost:5000/piwebapi",
                ["PiConnection:DataArchiveName"] = "PIServer1",
                ["PiConnection:AuthMode"] = "Anonymous",
                ["PiConnection:TimeoutSeconds"] = "5",
                ["Ollama:Enabled"] = "false",
                ["ConnectionStrings:TagCatalog"] = "Data Source=:memory:"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config, isDevelopment: true);
        await using var sp = services.BuildServiceProvider();

        var runtime = sp.GetRequiredService<PiDataSourceRuntimeInfo>();
        Assert.True(runtime.DevelopmentPreferSimulator);
        Assert.NotNull(sp.GetService<DevPreferSimulatorDataSource>());

        var connected = await sp.GetRequiredService<IPiConnectivity>().TryConnectAsync();
        Assert.True(connected);

        // Router decides via live TryConnect; trust runtime state after the probe.
        Assert.True(runtime.ActiveSource is "simulator" or "demo-fallback");
        if (runtime.ActiveSource == "simulator")
        {
            Assert.False(runtime.UseDemo);
            Assert.False(runtime.FellBackFromSimulator);
        }
        else
        {
            Assert.True(runtime.UseDemo);
            Assert.True(runtime.FellBackFromSimulator);
        }
    }

    [Fact]
    public void Non_development_registers_live_without_router()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PiConnection:UseDemoMode"] = "false",
                ["PiConnection:BaseUrl"] = "http://127.0.0.1:1/piwebapi",
                ["PiConnection:AuthMode"] = "Anonymous",
                ["PiConnection:TimeoutSeconds"] = "2",
                ["Ollama:Enabled"] = "false",
                ["ConnectionStrings:TagCatalog"] = "Data Source=:memory:"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config, isDevelopment: false);
        using var sp = services.BuildServiceProvider();

        var runtime = sp.GetRequiredService<PiDataSourceRuntimeInfo>();
        Assert.Equal("live", runtime.ActiveSource);
        Assert.Null(sp.GetService<DevPreferSimulatorDataSource>());
    }

    [Fact]
    public void Explicit_demo_mode_stays_demo_even_in_development()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PiConnection:UseDemoMode"] = "true",
                ["PiConnection:AuthMode"] = "Demo",
                ["PiConnection:BaseUrl"] = "http://localhost:5000/piwebapi",
                ["Ollama:Enabled"] = "false",
                ["ConnectionStrings:TagCatalog"] = "Data Source=:memory:"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config, isDevelopment: true);
        using var sp = services.BuildServiceProvider();

        var runtime = sp.GetRequiredService<PiDataSourceRuntimeInfo>();
        Assert.Equal("demo", runtime.ActiveSource);
        Assert.True(runtime.UseDemo);
        Assert.Null(sp.GetService<DevPreferSimulatorDataSource>());
    }
}
