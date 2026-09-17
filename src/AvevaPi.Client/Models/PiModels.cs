namespace AvevaPi.Client.Models;

/// <summary>
/// A PI Point is what most people call a "tag" — one named signal stored in the Data Archive.
/// </summary>
public sealed class PiPointInfo
{
    public required string Name { get; init; }
    public string? WebId { get; init; }
    public string? Path { get; init; }
    public string? Descriptor { get; init; }
    public string? PointType { get; init; }
    public string? EngineeringUnits { get; init; }
    public string? PointSource { get; init; }
    public string? InstrumentTag { get; init; }
    public string? DigitalSetName { get; init; }
    public string? Location1 { get; init; }
    public string? Location2 { get; init; }
    public string? Location3 { get; init; }
    public string? Location4 { get; init; }
    public string? Location5 { get; init; }
    public string? ScanClass { get; init; }
}

/// <summary>AF attribute metadata (may or may not map to a PI Point).</summary>
public sealed class PiAttributeInfo
{
    public required string Name { get; init; }
    public string? WebId { get; init; }
    public string? Path { get; init; }
    public string? Description { get; init; }
    public string? TypeName { get; init; }
    public string? DefaultUnitsName { get; init; }
    public string? DataReferencePlugIn { get; init; }
    public string? ConfigString { get; init; }
    public string? CategoryNames { get; init; }
    public string? ElementName { get; init; }
    public string? ElementPath { get; init; }
    public string? TemplateName { get; init; }
}

/// <summary>One timestamped value from a PI Point or AF attribute stream.</summary>
public sealed class PiTimedValue
{
    public required DateTimeOffset Timestamp { get; init; }
    public object? Value { get; init; }
    public string? UnitsAbbreviation { get; init; }
    public bool Good { get; init; } = true;
    public bool Questionable { get; init; }
    public bool Substituted { get; init; }
    public string? Status { get; init; }
}

/// <summary>Lightweight search hit used by resolver / search endpoints.</summary>
public sealed class PiSearchHit
{
    public required string Name { get; init; }
    public string? Path { get; init; }
    public string? WebId { get; init; }
    public string? Description { get; init; }
    public string Kind { get; init; } = "Point";
}

public sealed class PiWriteResult
{
    public required string TagName { get; init; }
    public bool Succeeded { get; init; }
    public string? Message { get; init; }
}
