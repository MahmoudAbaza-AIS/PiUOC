using PiAiAssistant.Domain.Entities;

namespace PiAiAssistant.Domain.Interfaces;

/// <summary>Connectivity probe for PI Web API (or demo stand-in).</summary>
public interface IPiConnectivity
{
    Task<bool> TryConnectAsync(CancellationToken cancellationToken = default);
    Task<string> GetSystemStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>Resolve and search PI Points.</summary>
public interface IPiPointReader
{
    Task<PiPoint?> FindByNameAsync(string tagName, CancellationToken cancellationToken = default);
    Task<PiPoint?> FindPointByPathAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PiSearchHit>> SearchByNameAsync(
        string nameFilter,
        int maxCount = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>Read current and recorded tag/stream values.</summary>
public interface ITagValueReader
{
    Task<TagSample?> GetCurrentByWebIdAsync(string webId, CancellationToken cancellationToken = default);
    Task<TagSample?> GetCurrentByNameAsync(string tagName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TagSample>> GetRecordedByWebIdAsync(
        string webId,
        string startTime = "*-1h",
        string endTime = "*",
        int maxCount = 100,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TagSample>> GetRecordedByNameAsync(
        string tagName,
        string startTime = "*-1h",
        string endTime = "*",
        int maxCount = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>Resolve AF attributes by path.</summary>
public interface IAfAttributeReader
{
    Task<AfAttribute?> FindAttributeByPathAsync(string path, CancellationToken cancellationToken = default);
}
