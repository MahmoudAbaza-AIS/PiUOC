using AvevaPi.Client.Models;
using AvevaPi.Client.Options;
using Microsoft.Extensions.Options;

namespace AvevaPi.Client;

/// <summary>
/// Offline stand-in for PI Web API used by local development and demos.
/// </summary>
public sealed class DemoPiDataClient : IPiDataClient
{
    private readonly Dictionary<string, DemoPoint> _points;
    private readonly Dictionary<string, DemoAttribute> _attributes;
    private readonly object _gate = new();

    public DemoPiDataClient(IOptions<PiConnectionOptions> options)
    {
        var names = options.Value.SampleTagNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .DefaultIfEmpty("SINUSOID")
            .ToArray();

        // Seed rich demo plant tags that match the Tag Intelligence examples.
        var seeded = new[]
        {
            DemoPoint.Create("SINUSOID", "Demo sine wave", "deg", "R", "CLASSIC"),
            DemoPoint.Create("CDT158", "Demo oscillating tag", "deg", "R", "CLASSIC"),
            DemoPoint.Create("BA:LEVEL.1", "Demo tank level", "%", "L", "CLASSIC"),
            DemoPoint.Create("B03_STEAM_PRESSURE", "Main steam pressure downstream of Boiler 03", "bar(g)", "OPC", "Plant1"),
            DemoPoint.Create("B03_STEAM_TEMP", "Main steam temperature at Boiler 03", "degC", "OPC", "Plant1"),
            DemoPoint.Create("B03_FEEDWATER_FLOW", "Boiler 03 feedwater flow", "t/h", "OPC", "Plant1"),
            DemoPoint.Create("B03_BURNER_LOAD", "Boiler 03 burner load", "%", "OPC", "Plant1"),
            DemoPoint.Create("B04_STEAM_PRESSURE", "Main steam pressure downstream of Boiler 04", "bar(g)", "OPC", "Plant1"),
            DemoPoint.Create("HEADER_STEAM_PRESSURE", "Main steam header pressure", "bar(g)", "OPC", "Plant1")
        };

        _points = new Dictionary<string, DemoPoint>(StringComparer.OrdinalIgnoreCase);
        foreach (var point in seeded)
        {
            _points[point.Name] = point;
        }

        foreach (var name in names)
        {
            if (!_points.ContainsKey(name))
            {
                _points[name] = DemoPoint.Create(name, $"Demo tag for {name}", "eng", "DEMO", "DEMO");
            }
        }

        _attributes = new Dictionary<string, DemoAttribute>(StringComparer.OrdinalIgnoreCase)
        {
            [@"\\AFSERVER\Production\Plant1\Boiler03|Steam Pressure"] = new(
                "Steam Pressure",
                @"\\AFSERVER\Production\Plant1\Boiler03|Steam Pressure",
                "Main steam pressure downstream of Boiler 03",
                "Float32",
                "bar(g)",
                "PI Point",
                @"\\DEMO\B03_STEAM_PRESSURE",
                "Boiler03",
                @"\\AFSERVER\Production\Plant1\Boiler03",
                "BoilerTemplate",
                "Process"),
            [@"\\AFSERVER\Production\Plant1\Boiler03|Steam Temperature"] = new(
                "Steam Temperature",
                @"\\AFSERVER\Production\Plant1\Boiler03|Steam Temperature",
                "Main steam temperature at Boiler 03",
                "Float32",
                "degC",
                "PI Point",
                @"\\DEMO\B03_STEAM_TEMP",
                "Boiler03",
                @"\\AFSERVER\Production\Plant1\Boiler03",
                "BoilerTemplate",
                "Process")
        };
    }

    public Task<string> GetSystemStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult("DEMO MODE — no real PI server is contacted.");

    public Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<PiPointInfo?> FindPointAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var key = tagName.Trim().TrimStart('\\');
        if (key.Contains('\\'))
        {
            key = key.Split('\\').Last();
        }

        return Task.FromResult(MapPoint(key));
    }

    public Task<PiPointInfo?> FindPointByPathAsync(string path, CancellationToken cancellationToken = default)
        => FindPointAsync(path, cancellationToken);

    public Task<PiAttributeInfo?> FindAttributeByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_attributes.TryGetValue(path.Trim(), out var attr) ||
            _attributes.Keys.FirstOrDefault(k => k.EndsWith(path.Trim(), StringComparison.OrdinalIgnoreCase)) is { } matched
                && _attributes.TryGetValue(matched, out attr))
        {
            return Task.FromResult<PiAttributeInfo?>(new PiAttributeInfo
            {
                Name = attr.Name,
                WebId = $"DEMO_ATTR_{attr.Name.Replace(' ', '_')}",
                Path = attr.Path,
                Description = attr.Description,
                TypeName = attr.TypeName,
                DefaultUnitsName = attr.Units,
                DataReferencePlugIn = attr.DataReference,
                ConfigString = attr.ConfigString,
                CategoryNames = attr.Category,
                ElementName = attr.ElementName,
                ElementPath = attr.ElementPath,
                TemplateName = attr.TemplateName
            });
        }

        return Task.FromResult<PiAttributeInfo?>(null);
    }

    public Task<IReadOnlyList<PiSearchHit>> SearchPointsAsync(
        string nameFilter,
        int maxCount = 10,
        CancellationToken cancellationToken = default)
    {
        var filter = nameFilter.Trim().Trim('*');
        var hits = _points.Values
            .Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || (p.Descriptor?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(Math.Clamp(maxCount, 1, 50))
            .Select(p => new PiSearchHit
            {
                Name = p.Name,
                Path = $@"\\DEMO\{p.Name}",
                WebId = $"DEMO_{p.Name}",
                Description = p.Descriptor,
                Kind = "Point"
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<PiSearchHit>>(hits);
    }

    public Task<PiTimedValue?> GetCurrentValueByWebIdAsync(string webId, CancellationToken cancellationToken = default)
    {
        var name = webId.Replace("DEMO_", "", StringComparison.OrdinalIgnoreCase);
        return GetCurrentValueAsync(name, cancellationToken);
    }

    public Task<PiTimedValue?> GetCurrentValueAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var point = RequirePoint(tagName);
        lock (_gate)
        {
            var value = point.LastWrittenValue ?? ComputeSinusoid(point.Seed, DateTimeOffset.UtcNow);
            return Task.FromResult<PiTimedValue?>(new PiTimedValue
            {
                Timestamp = DateTimeOffset.UtcNow,
                Value = Math.Round(value, 3),
                UnitsAbbreviation = point.EngineeringUnits,
                Good = true,
                Status = "Good"
            });
        }
    }

    public Task<IReadOnlyList<PiTimedValue>> GetRecordedValuesByWebIdAsync(
        string webId,
        string startTime = "*-1h",
        string endTime = "*",
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        var name = webId.Replace("DEMO_", "", StringComparison.OrdinalIgnoreCase);
        return GetRecordedValuesAsync(name, startTime, endTime, maxCount, cancellationToken);
    }

    public Task<IReadOnlyList<PiTimedValue>> GetRecordedValuesAsync(
        string tagName,
        string startTime = "*-1h",
        string endTime = "*",
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        var point = RequirePoint(tagName);
        var end = DateTimeOffset.UtcNow;
        var start = end.AddHours(-1);
        var count = Math.Clamp(maxCount, 1, 500);
        var step = TimeSpan.FromTicks(Math.Max(1, (end - start).Ticks / count));

        var values = new List<PiTimedValue>(count);
        for (var i = 0; i < count; i++)
        {
            var ts = start + TimeSpan.FromTicks(step.Ticks * i);
            values.Add(new PiTimedValue
            {
                Timestamp = ts,
                Value = Math.Round(ComputeSinusoid(point.Seed, ts), 3),
                UnitsAbbreviation = point.EngineeringUnits,
                Good = true,
                Status = "Good"
            });
        }

        return Task.FromResult<IReadOnlyList<PiTimedValue>>(values);
    }

    private PiPointInfo? MapPoint(string key)
    {
        if (!_points.TryGetValue(key, out var point))
        {
            return null;
        }

        return new PiPointInfo
        {
            Name = point.Name,
            WebId = $"DEMO_{point.Name}",
            Path = $@"\\DEMO\{point.Name}",
            Descriptor = point.Descriptor,
            PointType = "Float32",
            EngineeringUnits = point.EngineeringUnits,
            PointSource = point.PointSource,
            InstrumentTag = point.InstrumentTag,
            Location1 = point.Location1,
            ScanClass = "1 sec"
        };
    }

    private DemoPoint RequirePoint(string tagName)
    {
        var key = tagName.Trim().TrimStart('\\');
        if (key.Contains('\\'))
        {
            key = key.Split('\\').Last();
        }

        if (!_points.TryGetValue(key, out var point))
        {
            throw new InvalidOperationException($"Demo PI Point '{tagName}' was not found.");
        }

        return point;
    }

    private static double ComputeSinusoid(int seed, DateTimeOffset timestamp)
    {
        var radians = timestamp.ToUnixTimeSeconds() / 60.0 + seed;
        return 50 + 40 * Math.Sin(radians);
    }

    private sealed class DemoPoint
    {
        public required string Name { get; init; }
        public string? Descriptor { get; init; }
        public string EngineeringUnits { get; init; } = "eng";
        public string PointSource { get; init; } = "DEMO";
        public string? InstrumentTag { get; init; }
        public string? Location1 { get; init; }
        public int Seed { get; init; }
        public double? LastWrittenValue { get; set; }

        public static DemoPoint Create(string name, string descriptor, string units, string source, string location1)
            => new()
            {
                Name = name,
                Descriptor = descriptor,
                EngineeringUnits = units,
                PointSource = source,
                InstrumentTag = name,
                Location1 = location1,
                Seed = Math.Abs(name.GetHashCode(StringComparison.OrdinalIgnoreCase))
            };
    }

    private sealed record DemoAttribute(
        string Name,
        string Path,
        string Description,
        string TypeName,
        string Units,
        string DataReference,
        string ConfigString,
        string ElementName,
        string ElementPath,
        string TemplateName,
        string Category);
}
