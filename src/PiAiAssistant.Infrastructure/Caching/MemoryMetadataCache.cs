using Microsoft.Extensions.Caching.Memory;
using PiAiAssistant.Application.Abstractions;

namespace PiAiAssistant.Infrastructure.Caching;

public sealed class MemoryMetadataCache(IMemoryCache cache) : IMetadataCache
{
    public bool TryGet<T>(string key, out T? value)
    {
        if (cache.TryGetValue(key, out var boxed) && boxed is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan ttl)
        => cache.Set(key, value, ttl);
}
