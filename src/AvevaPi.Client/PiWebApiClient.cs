using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AvevaPi.Client.Models;
using AvevaPi.Client.Options;
using Microsoft.Extensions.Options;

namespace AvevaPi.Client;

/// <summary>
/// Talks to AVEVA PI System through PI Web API (HTTPS REST). Read-only for the assistant.
/// </summary>
public sealed class PiWebApiClient : IPiDataClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly PiConnectionOptions _options;

    public PiWebApiClient(HttpClient http, IOptions<PiConnectionOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> GetSystemStatusAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("", cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return $"Connected to PI Web API at {_options.BaseUrl}. Home payload length: {payload.Length} chars.";
    }

    public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public Task<PiPointInfo?> FindPointAsync(string tagName, CancellationToken cancellationToken = default)
        => FindPointByPathAsync(BuildPointPath(tagName), cancellationToken);

    public async Task<PiPointInfo?> FindPointByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var url = $"points?path={Uri.EscapeDataString(NormalizePath(path))}";
        using var response = await _http.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<PointDto>(JsonOptions, cancellationToken);
        return dto is null ? null : MapPoint(dto);
    }

    public async Task<PiAttributeInfo?> FindAttributeByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var url = $"attributes?path={Uri.EscapeDataString(NormalizePath(path))}";
        using var response = await _http.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AttributeDto>(JsonOptions, cancellationToken);
        return dto is null ? null : MapAttribute(dto);
    }

    public async Task<IReadOnlyList<PiSearchHit>> SearchPointsAsync(
        string nameFilter,
        int maxCount = 10,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"points/search?nameFilter={Uri.EscapeDataString(nameFilter)}" +
            $"&maxCount={Math.Clamp(maxCount, 1, 50)}";

        using var response = await _http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Fallback used by some PI Web API versions
            url =
                $"dataservers?path={Uri.EscapeDataString($"\\\\{_options.DataArchiveName}")}";
            return Array.Empty<PiSearchHit>();
        }

        var dto = await response.Content.ReadFromJsonAsync<ItemsDto<PointDto>>(JsonOptions, cancellationToken);
        if (dto?.Items is null)
        {
            return Array.Empty<PiSearchHit>();
        }

        return dto.Items
            .Select(p => new PiSearchHit
            {
                Name = p.Name ?? string.Empty,
                Path = p.Path,
                WebId = p.WebId,
                Description = p.Descriptor,
                Kind = "Point"
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToList();
    }

    public async Task<PiTimedValue?> GetCurrentValueByWebIdAsync(string webId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"streams/{Uri.EscapeDataString(webId)}/value", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ValueDto>(JsonOptions, cancellationToken);
        return dto is null ? null : MapValue(dto);
    }

    public async Task<PiTimedValue?> GetCurrentValueAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var point = await FindPointAsync(tagName, cancellationToken)
            ?? throw new InvalidOperationException($"PI Point '{tagName}' was not found on '{_options.DataArchiveName}'.");

        if (string.IsNullOrWhiteSpace(point.WebId))
        {
            return null;
        }

        return await GetCurrentValueByWebIdAsync(point.WebId, cancellationToken);
    }

    public async Task<IReadOnlyList<PiTimedValue>> GetRecordedValuesByWebIdAsync(
        string webId,
        string startTime = "*-1h",
        string endTime = "*",
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"streams/{Uri.EscapeDataString(webId)}/recorded" +
            $"?startTime={Uri.EscapeDataString(startTime)}" +
            $"&endTime={Uri.EscapeDataString(endTime)}" +
            $"&maxCount={maxCount}";

        using var response = await _http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ItemsDto<ValueDto>>(JsonOptions, cancellationToken);
        if (dto?.Items is null)
        {
            return Array.Empty<PiTimedValue>();
        }

        return dto.Items.Select(MapValue).ToList();
    }

    public async Task<IReadOnlyList<PiTimedValue>> GetRecordedValuesAsync(
        string tagName,
        string startTime = "*-1h",
        string endTime = "*",
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        var point = await FindPointAsync(tagName, cancellationToken)
            ?? throw new InvalidOperationException($"PI Point '{tagName}' was not found on '{_options.DataArchiveName}'.");

        if (string.IsNullOrWhiteSpace(point.WebId))
        {
            return Array.Empty<PiTimedValue>();
        }

        return await GetRecordedValuesByWebIdAsync(point.WebId, startTime, endTime, maxCount, cancellationToken);
    }

    public static void ConfigureHttpClient(HttpClient client, PiConnectionOptions options)
    {
        var baseUrl = options.BaseUrl.TrimEnd('/') + "/";
        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));

        if (string.Equals(options.AuthMode, "Basic", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(options.Username))
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        }
    }

    public static HttpMessageHandler CreateHandler(PiConnectionOptions options)
    {
        var handler = new HttpClientHandler();

        if (string.Equals(options.AuthMode, "Windows", StringComparison.OrdinalIgnoreCase)
            || string.Equals(options.AuthMode, "DefaultCredentials", StringComparison.OrdinalIgnoreCase))
        {
            handler.UseDefaultCredentials = true;
        }

        if (string.Equals(options.AuthMode, "NetworkCredential", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(options.Username))
        {
            handler.Credentials = new NetworkCredential(options.Username, options.Password, options.Domain);
        }

        if (options.AcceptInvalidCertificates)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return handler;
    }

    private string BuildPointPath(string tagName)
    {
        var trimmed = tagName.Trim().TrimStart('\\');
        if (trimmed.Contains('\\', StringComparison.Ordinal))
        {
            return "\\\\" + trimmed;
        }

        return $"\\\\{_options.DataArchiveName}\\{trimmed}";
    }

    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim();
        if (trimmed.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return trimmed;
        }

        if (trimmed.StartsWith('\\'))
        {
            return "\\" + trimmed;
        }

        return trimmed;
    }

    private static PiPointInfo MapPoint(PointDto dto) => new()
    {
        Name = dto.Name ?? string.Empty,
        WebId = dto.WebId,
        Path = dto.Path,
        Descriptor = dto.Descriptor,
        PointType = dto.PointType,
        EngineeringUnits = dto.EngineeringUnits,
        PointSource = dto.PointSource,
        InstrumentTag = dto.InstrumentTag,
        DigitalSetName = dto.DigitalSetName,
        Location1 = dto.Location1,
        Location2 = dto.Location2,
        Location3 = dto.Location3,
        Location4 = dto.Location4,
        Location5 = dto.Location5,
        ScanClass = dto.Future.HasValue ? null : dto.Span?.ToString(CultureInfo.InvariantCulture)
    };

    private static PiAttributeInfo MapAttribute(AttributeDto dto)
    {
        string? elementPath = null;
        string? elementName = null;
        if (!string.IsNullOrWhiteSpace(dto.Path))
        {
            var pipe = dto.Path.LastIndexOf('|');
            elementPath = pipe > 0 ? dto.Path[..pipe] : dto.Path;
            elementName = elementPath?.Split('\\', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        }

        return new PiAttributeInfo
        {
            Name = dto.Name ?? string.Empty,
            WebId = dto.WebId,
            Path = dto.Path,
            Description = dto.Description,
            TypeName = dto.TypeName,
            DefaultUnitsName = dto.DefaultUnitsName,
            DataReferencePlugIn = dto.DataReferencePlugIn,
            ConfigString = dto.ConfigString,
            CategoryNames = dto.CategoryNames is { Length: > 0 } ? string.Join(", ", dto.CategoryNames) : null,
            ElementName = elementName,
            ElementPath = elementPath,
            TemplateName = dto.TemplateName
        };
    }

    private static PiTimedValue MapValue(ValueDto dto)
    {
        var timestamp = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Timestamp) &&
            DateTimeOffset.TryParse(dto.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            timestamp = parsed;
        }

        var good = dto.Good ?? true;
        return new PiTimedValue
        {
            Timestamp = timestamp,
            Value = UnwrapJsonElement(dto.Value),
            UnitsAbbreviation = dto.UnitsAbbreviation,
            Good = good,
            Questionable = dto.Questionable ?? false,
            Substituted = dto.Substituted ?? false,
            Status = good ? "Good" : (dto.Annotated ? "Annotated/Bad" : "Bad")
        };
    }

    private static object? UnwrapJsonElement(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number when element.TryGetDouble(out var d) => d,
                JsonValueKind.String => element.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.ToString()
            };
        }

        return value;
    }

    private sealed class PointDto
    {
        public string? WebId { get; set; }
        public string? Name { get; set; }
        public string? Path { get; set; }
        public string? Descriptor { get; set; }
        public string? PointType { get; set; }
        public string? EngineeringUnits { get; set; }
        public string? PointSource { get; set; }
        public string? InstrumentTag { get; set; }
        public string? DigitalSetName { get; set; }
        public string? Location1 { get; set; }
        public string? Location2 { get; set; }
        public string? Location3 { get; set; }
        public string? Location4 { get; set; }
        public string? Location5 { get; set; }
        public double? Span { get; set; }
        public bool? Future { get; set; }
    }

    private sealed class AttributeDto
    {
        public string? WebId { get; set; }
        public string? Name { get; set; }
        public string? Path { get; set; }
        public string? Description { get; set; }
        public string? TypeName { get; set; }
        public string? DefaultUnitsName { get; set; }
        public string? DataReferencePlugIn { get; set; }
        public string? ConfigString { get; set; }
        public string[]? CategoryNames { get; set; }
        public string? TemplateName { get; set; }
    }

    private sealed class ValueDto
    {
        public string? Timestamp { get; set; }
        public object? Value { get; set; }
        public string? UnitsAbbreviation { get; set; }
        public bool? Good { get; set; }
        public bool? Questionable { get; set; }
        public bool? Substituted { get; set; }
        public bool Annotated { get; set; }
    }

    private sealed class ItemsDto<T>
    {
        public List<T>? Items { get; set; }
    }
}
