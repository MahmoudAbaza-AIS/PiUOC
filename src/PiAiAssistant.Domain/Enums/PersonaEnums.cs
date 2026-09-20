namespace PiAiAssistant.Domain.Enums;

/// <summary>Role-driven assistant personas (PI Vision AI guideline).</summary>
public enum AssistantPersona
{
    Executive,
    SectorOps,
    PlantManager,
    ShiftOperator,
    ReliabilityEngineer
}

public enum GenerationTechnology
{
    Combined,
    Gas,
    Steam,
    Unknown
}

public enum PlantHealthState
{
    Healthy,
    DataUnhealthy,
    Unavailable
}
