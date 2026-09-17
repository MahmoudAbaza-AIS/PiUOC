using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PiAiAssistant.Domain.Interfaces;
using PiAiAssistant.Infrastructure;
using PiAiAssistant.Infrastructure.Options;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Configuration.AddEnvironmentVariables(prefix: "AVEVAPI_");
builder.Services.AddInfrastructure(builder.Configuration);

using var host = builder.Build();
await host.Services.InitializeInfrastructureAsync();

var pi = host.Services.GetRequiredService<IPiConnectivity>();
var points = host.Services.GetRequiredService<IPiPointReader>();
var values = host.Services.GetRequiredService<ITagValueReader>();
var settings = host.Services.GetRequiredService<IOptions<PiConnectionOptions>>().Value;

Console.WriteLine("=== AVEVA PI smoke console (Clean Architecture) ===");
Console.WriteLine($"Mode: {(settings.UseDemoMode ? "DEMO" : "LIVE")}");
Console.WriteLine(await pi.GetSystemStatusAsync());
Console.WriteLine();

var tags = settings.SampleTagNames.Length > 0
    ? settings.SampleTagNames
    : ["SINUSOID", "B03_STEAM_PRESSURE"];

foreach (var tag in tags)
{
    var point = await points.FindByNameAsync(tag);
    if (point is null)
    {
        Console.WriteLine($"{tag}: not found");
        continue;
    }

    var current = await values.GetCurrentByNameAsync(tag);
    Console.WriteLine($"{point.Name}: {current?.Value} {current?.UnitsAbbreviation} ({current?.Status})");
}

Console.WriteLine();
Console.WriteLine("API host: dotnet run --project src/PiAiAssistant.Api");
