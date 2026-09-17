namespace PiAiAssistant.Application.Abstractions;

/// <summary>Application-level cache port (Infrastructure may back this with MemoryCache).</summary>
public interface IMetadataCache
{
    bool TryGet<T>(string key, out T? value);
    void Set<T>(string key, T value, TimeSpan ttl);
}
