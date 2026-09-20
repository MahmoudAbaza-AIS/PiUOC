using PiAiAssistant.Domain.Entities;

namespace PiAiAssistant.Domain.Interfaces;

/// <summary>Read AFAG business hierarchy (Plant/Block/Unit/KPI) — never expose raw tags here.</summary>
public interface IAfagHierarchyReader
{
    Task<Sector> GetKingdomAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Sector>> GetSectorsAsync(CancellationToken cancellationToken = default);
    Task<Sector?> GetSectorAsync(string sectorCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Plant>> GetPlantsAsync(string? sectorCode = null, CancellationToken cancellationToken = default);
    Task<Plant?> GetPlantAsync(string plantCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlantBlock>> GetBlocksAsync(string plantCode, CancellationToken cancellationToken = default);
    Task<GenerationUnit?> GetUnitAsync(string plantCode, string blockCode, string unitCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GenerationUnit>> GetUnitsAsync(string plantCode, string? blockCode = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KpiSnapshot>> GetKpisAsync(string scope, string? code = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(DateTimeOffset TimestampUtc, double GenMwh, double NaturalGasKg, double LiquidFuelKg)>> GetPlantFuelTrendAsync(
        string plantCode,
        int days = 7,
        CancellationToken cancellationToken = default);
}

/// <summary>Curated SOP / manual knowledge (RAG-ready port; demo uses in-memory excerpts).</summary>
public interface ISopKnowledgeStore
{
    Task<IReadOnlyList<SopExcerpt>> SearchAsync(string query, int maxResults = 3, CancellationToken cancellationToken = default);
    Task<SopExcerpt?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
}
