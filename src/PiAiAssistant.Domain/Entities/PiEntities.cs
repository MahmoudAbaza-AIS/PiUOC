namespace PiAiAssistant.Domain.Entities;

/// <summary>PI Point (historian tag) metadata.</summary>
public sealed class PiPoint
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

/// <summary>AF attribute metadata (may map to a PI Point).</summary>
public sealed class AfAttribute
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

/// <summary>One timestamped process value.</summary>
public sealed class TagSample
{
    public required DateTimeOffset TimestampUtc { get; init; }
    public object? Value { get; init; }
    public string? UnitsAbbreviation { get; init; }
    public bool IsGood { get; init; } = true;
    public bool IsQuestionable { get; init; }
    public bool IsSubstituted { get; init; }
    public string? Status { get; init; }
}

/// <summary>Lightweight search hit from PI or catalog.</summary>
public sealed class PiSearchHit
{
    public required string Name { get; init; }
    public string? Path { get; init; }
    public string? WebId { get; init; }
    public string? Description { get; init; }
    public string Kind { get; init; } = "Point";
}
