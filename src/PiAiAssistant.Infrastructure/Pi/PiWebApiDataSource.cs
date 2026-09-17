using PiAiAssistant.Application.Exceptions;
using PiAiAssistant.Domain.Entities;
using PiAiAssistant.Domain.Exceptions;
using PiAiAssistant.Domain.Interfaces;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PiAiAssistant.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace PiAiAssistant.Infrastructure.Pi;

/// <summary>
/// Talks to AVEVA PI System through PI Web API (HTTPS REST). Read-only for the assistant.
/// HTTP/auth failures are translated to Application/Domain exceptions before leaving Infrastructure.
/// </summary>
public sealed class PiWebApiDataSource : IPiConnectivity, IPiPointReader, ITagValueReader, IAfAttributeReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly PiConnectionOptions _options;

    public PiWebApiDataSource(HttpClient http, IOptions<PiConnectionOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> GetSystemStatusAsync(CancellationToken cancellationToken = default)
    {
        using var response = await GetHomeOrCatalogAsync(cancellationToken);
        EnsurePiSuccess(response, "home");
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return $"Connected to PI Web API at {_options.BaseUrl.TrimEnd('/')}. Payload length: {payload.Length} chars.";
    }

    public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await GetHomeOrCatalogAsync(cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// HttpClient BaseAddress is typically .../piwebapi/, so GetAsync("") becomes .../piwebapi/ (404 on the Flask emulator).
    /// Prefer absolute home without a trailing slash, then fall back to dataservers.
    /// </summary>
    private async Task<HttpResponseMessage> GetHomeOrCatalogAsync(CancellationToken cancellationToken)
    {
        var homeNoSlash = new Uri(_options.BaseUrl.TrimEnd('/'));
        var homeResponse = await _http.GetAsync(homeNoSlash, cancellationToken);
        if (homeResponse.IsSuccessStatusCode)
        {
            return homeResponse;
        }

        homeResponse.Dispose();
        return await _http.GetAsync("dataservers", cancellationToken);
    }

    public Task<PiPoint?> FindByNameAsync(string tagName, CancellationToken cancellationToken = default)
        => FindPointByPathAsync(BuildPointPath(tagName), cancellationToken);

    public async Task<PiPoint?> FindPointByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePath(path);
        var url = $"points?path={Uri.EscapeDataString(normalized)}";
        using (var response = await _http.GetAsync(url, cancellationToken))
        {
            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync<PointDto>(JsonOptions, cancellationToken);
                if (dto is not null)
                {
                    return MapPoint(dto);
                }
            }
            else if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                EnsurePiSuccess(response, "points");
            }
        }

        // Emulators / some PI Web API installs lack GetByPath — resolve by WebId or archive point list.
        var tagName = ExtractPointName(normalized);
        if (!string.IsNullOrWhiteSpace(tagName))
        {
            var byWebId = await TryGetPointByWebIdAsync($"pt_{tagName}", cancellationToken);
            if (byWebId is not null)
            {
                return byWebId;
            }
        }

        var archivePoints = await ListArchivePointsAsync(cancellationToken);
        return archivePoints.FirstOrDefault(p =>
            string.Equals(p.Path, normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Name, tagName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<AfAttribute?> FindAttributeByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePath(path);
        var url = $"attributes?path={Uri.EscapeDataString(normalized)}";
        using (var response = await _http.GetAsync(url, cancellationToken))
        {
            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync<AttributeDto>(JsonOptions, cancellationToken);
                if (dto is not null)
                {
                    return MapAttribute(dto);
                }
            }
            else if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                EnsurePiSuccess(response, "attributes");
            }
        }

        var guessedId = GuessEmulatorAttributeWebId(normalized);
        if (!string.IsNullOrWhiteSpace(guessedId))
        {
            var byId = await TryGetAttributeByWebIdAsync(guessedId, cancellationToken);
            if (byId is not null)
            {
                return byId;
            }
        }

        return await FindAttributeByWalkingAfAsync(normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<PiSearchHit>> SearchByNameAsync(
        string nameFilter,
        int maxCount = 10,
        CancellationToken cancellationToken = default)
    {
        maxCount = Math.Clamp(maxCount, 1, 50);
        var url =
            $"points/search?nameFilter={Uri.EscapeDataString(nameFilter)}" +
            $"&maxCount={maxCount}";

        using (var response = await _http.GetAsync(url, cancellationToken))
        {
            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync<ItemsDto<PointDto>>(JsonOptions, cancellationToken);
                if (dto?.Items is { Count: > 0 })
                {
                    return dto.Items
                        .Select(ToSearchHit)
                        .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                        .Take(maxCount)
                        .ToList();
                }
            }
            else if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                EnsurePiSuccess(response, "points/search");
            }
        }

        var filter = nameFilter.Trim().Trim('*');
        var archivePoints = await ListArchivePointsAsync(cancellationToken);
        return archivePoints
            .Where(p =>
                (!string.IsNullOrWhiteSpace(p.Name) && p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(p.Descriptor) && p.Descriptor.Contains(filter, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(p.Path) && p.Path.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            .Take(maxCount)
            .Select(ToSearchHit)
            .ToList();
    }

    public async Task<TagSample?> GetCurrentByWebIdAsync(string webId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"streams/{Uri.EscapeDataString(webId)}/value", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsurePiSuccess(response, "streams/value");
        var dto = await response.Content.ReadFromJsonAsync<ValueDto>(JsonOptions, cancellationToken);
        return dto is null ? null : MapValue(dto);
    }

    public async Task<TagSample?> GetCurrentByNameAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var point = await FindByNameAsync(tagName, cancellationToken)
            ?? throw new TagNotFoundException(tagName);

        if (string.IsNullOrWhiteSpace(point.WebId))
        {
            return null;
        }

        return await GetCurrentByWebIdAsync(point.WebId, cancellationToken);
    }

    public async Task<IReadOnlyList<TagSample>> GetRecordedByWebIdAsync(
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
        EnsurePiSuccess(response, "streams/recorded");
        var dto = await response.Content.ReadFromJsonAsync<ItemsDto<ValueDto>>(JsonOptions, cancellationToken);
        if (dto?.Items is null)
        {
            return Array.Empty<TagSample>();
        }

        return dto.Items.Select(MapValue).ToList();
    }

    public async Task<IReadOnlyList<TagSample>> GetRecordedByNameAsync(
        string tagName,
        string startTime = "*-1h",
        string endTime = "*",
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        var point = await FindByNameAsync(tagName, cancellationToken)
            ?? throw new TagNotFoundException(tagName);

        if (string.IsNullOrWhiteSpace(point.WebId))
        {
            return Array.Empty<TagSample>();
        }

        return await GetRecordedByWebIdAsync(point.WebId, startTime, endTime, maxCount, cancellationToken);
    }

    /// <summary>Maps PI Web API HTTP failures to Application-layer exceptions.</summary>
    internal static void EnsurePiSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var code = (int)response.StatusCode;
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new PiAuthenticationException(
                $"PI Web API {operation} failed with HTTP {code} ({response.ReasonPhrase}).");
        }

        throw new PiConnectionException(
            $"PI Web API {operation} failed with HTTP {code} ({response.ReasonPhrase}).");
    }

    private async Task<PiPoint?> TryGetPointByWebIdAsync(string webId, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync($"points/{Uri.EscapeDataString(webId)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var dto = await response.Content.ReadFromJsonAsync<PointDto>(JsonOptions, cancellationToken);
        return dto is null ? null : MapPoint(dto);
    }

    private async Task<AfAttribute?> TryGetAttributeByWebIdAsync(string webId, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync($"attributes/{Uri.EscapeDataString(webId)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var dto = await response.Content.ReadFromJsonAsync<AttributeDto>(JsonOptions, cancellationToken);
        return dto is null ? null : MapAttribute(dto);
    }

    private async Task<IReadOnlyList<PiPoint>> ListArchivePointsAsync(CancellationToken cancellationToken)
    {
        using var serversResponse = await _http.GetAsync("dataservers", cancellationToken);
        if (!serversResponse.IsSuccessStatusCode)
        {
            return [];
        }

        var servers = await serversResponse.Content.ReadFromJsonAsync<ItemsDto<NamedWebIdDto>>(JsonOptions, cancellationToken);
        var server = servers?.Items?.FirstOrDefault(s =>
                         string.Equals(s.Name, _options.DataArchiveName, StringComparison.OrdinalIgnoreCase))
                     ?? servers?.Items?.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(server?.WebId))
        {
            return [];
        }

        using var pointsResponse = await _http.GetAsync(
            $"dataservers/{Uri.EscapeDataString(server.WebId)}/points",
            cancellationToken);
        if (!pointsResponse.IsSuccessStatusCode)
        {
            return [];
        }

        var points = await pointsResponse.Content.ReadFromJsonAsync<ItemsDto<PointDto>>(JsonOptions, cancellationToken);
        return points?.Items?.Select(MapPoint).ToList() ?? [];
    }

    private async Task<AfAttribute?> FindAttributeByWalkingAfAsync(string normalizedPath, CancellationToken cancellationToken)
    {
        using var serversResponse = await _http.GetAsync("assetservers", cancellationToken);
        if (!serversResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var servers = await serversResponse.Content.ReadFromJsonAsync<ItemsDto<NamedWebIdDto>>(JsonOptions, cancellationToken);
        var server = servers?.Items?.FirstOrDefault(s =>
                         string.Equals(s.Name, _options.DefaultAfServer, StringComparison.OrdinalIgnoreCase))
                     ?? servers?.Items?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(server?.WebId))
        {
            return null;
        }

        using var databasesResponse = await _http.GetAsync(
            $"assetservers/{Uri.EscapeDataString(server.WebId)}/databases",
            cancellationToken);
        if (!databasesResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var databases = await databasesResponse.Content.ReadFromJsonAsync<ItemsDto<NamedWebIdDto>>(JsonOptions, cancellationToken);
        var database = databases?.Items?.FirstOrDefault(d =>
                           string.Equals(d.Name, _options.DefaultAfDatabase, StringComparison.OrdinalIgnoreCase))
                       ?? databases?.Items?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(database?.WebId))
        {
            return null;
        }

        using var rootsResponse = await _http.GetAsync(
            $"databases/{Uri.EscapeDataString(database.WebId)}/elements",
            cancellationToken);
        if (!rootsResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var roots = await rootsResponse.Content.ReadFromJsonAsync<ItemsDto<NamedWebIdDto>>(JsonOptions, cancellationToken);
        if (roots?.Items is null)
        {
            return null;
        }

        var queue = new Queue<string>(roots.Items.Where(i => !string.IsNullOrWhiteSpace(i.WebId)).Select(i => i.WebId!));
        var visited = 0;
        const int maxVisit = 200;

        while (queue.Count > 0 && visited < maxVisit)
        {
            visited++;
            var elementWebId = queue.Dequeue();

            using (var attrsResponse = await _http.GetAsync(
                       $"elements/{Uri.EscapeDataString(elementWebId)}/attributes",
                       cancellationToken))
            {
                if (attrsResponse.IsSuccessStatusCode)
                {
                    var attrs = await attrsResponse.Content.ReadFromJsonAsync<ItemsDto<AttributeDto>>(JsonOptions, cancellationToken);
                    var match = attrs?.Items?.FirstOrDefault(a =>
                        string.Equals(a.Path, normalizedPath, StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                    {
                        return MapAttribute(match);
                    }
                }
            }

            using var childrenResponse = await _http.GetAsync(
                $"elements/{Uri.EscapeDataString(elementWebId)}/elements",
                cancellationToken);
            if (!childrenResponse.IsSuccessStatusCode)
            {
                continue;
            }

            var children = await childrenResponse.Content.ReadFromJsonAsync<ItemsDto<NamedWebIdDto>>(JsonOptions, cancellationToken);
            foreach (var child in children?.Items ?? [])
            {
                if (!string.IsNullOrWhiteSpace(child.WebId))
                {
                    queue.Enqueue(child.WebId);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// NuGreen emulator attribute WebIds look like A_Houston_B-210_Temperature.
    /// </summary>
    private static string? GuessEmulatorAttributeWebId(string normalizedPath)
    {
        var pipe = normalizedPath.LastIndexOf('|');
        if (pipe <= 0 || pipe >= normalizedPath.Length - 1)
        {
            return null;
        }

        var attr = normalizedPath[(pipe + 1)..].Replace(" ", string.Empty, StringComparison.Ordinal);
        var elementParts = normalizedPath[..pipe]
            .TrimStart('\\')
            .Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (elementParts.Length < 3)
        {
            return null;
        }

        // \\AFServer1\NuGreen\Houston\...\B-210|Temperature → site=Houston, equipment=B-210
        var site = elementParts.Length > 2 ? elementParts[2] : elementParts[0];
        var equipment = elementParts[^1];
        return $"A_{site}_{equipment}_{attr}";
    }

    private static string ExtractPointName(string normalizedPath)
    {
        var trimmed = normalizedPath.Trim().TrimStart('\\');
        return trimmed.Contains('\\', StringComparison.Ordinal)
            ? trimmed.Split('\\', StringSplitOptions.RemoveEmptyEntries).Last()
            : trimmed;
    }

    private static PiSearchHit ToSearchHit(PointDto p) => new()
    {
        Name = p.Name ?? string.Empty,
        Path = p.Path,
        WebId = p.WebId,
        Description = p.Descriptor,
        Kind = "Point"
    };

    private static PiSearchHit ToSearchHit(PiPoint p) => new()
    {
        Name = p.Name,
        Path = p.Path,
        WebId = p.WebId,
        Description = p.Descriptor,
        Kind = "Point"
    };

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

    private static PiPoint MapPoint(PointDto dto) => new()
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

    private static AfAttribute MapAttribute(AttributeDto dto)
    {
        string? elementPath = null;
        string? elementName = null;
        if (!string.IsNullOrWhiteSpace(dto.Path))
        {
            var pipe = dto.Path.LastIndexOf('|');
            elementPath = pipe > 0 ? dto.Path[..pipe] : dto.Path;
            elementName = elementPath?.Split('\\', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        }

        return new AfAttribute
        {
            Name = dto.Name ?? string.Empty,
            WebId = dto.WebId,
            Path = dto.Path,
            Description = dto.Description,
            TypeName = dto.TypeName ?? dto.Type,
            DefaultUnitsName = dto.DefaultUnitsName,
            DataReferencePlugIn = dto.DataReferencePlugIn,
            ConfigString = dto.ConfigString ?? dto.Links?.Point,
            CategoryNames = dto.CategoryNames is { Length: > 0 } ? string.Join(", ", dto.CategoryNames) : null,
            ElementName = elementName,
            ElementPath = elementPath,
            TemplateName = dto.TemplateName
        };
    }

    private static TagSample MapValue(ValueDto dto)
    {
        var timestamp = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Timestamp) &&
            DateTimeOffset.TryParse(dto.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            timestamp = parsed;
        }

        var good = dto.Good ?? true;
        return new TagSample
        {
            TimestampUtc = timestamp,
            Value = UnwrapJsonElement(dto.Value),
            UnitsAbbreviation = dto.UnitsAbbreviation,
            IsGood = good,
            IsQuestionable = dto.Questionable ?? false,
            IsSubstituted = dto.Substituted ?? false,
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
        public string? Type { get; set; }
        public string? DefaultUnitsName { get; set; }
        public string? DataReferencePlugIn { get; set; }
        public string? ConfigString { get; set; }
        public string[]? CategoryNames { get; set; }
        public string? TemplateName { get; set; }
        public LinksDto? Links { get; set; }
    }

    private sealed class LinksDto
    {
        public string? Point { get; set; }
    }

    private sealed class NamedWebIdDto
    {
        public string? Name { get; set; }
        public string? WebId { get; set; }
        public string? Path { get; set; }
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
