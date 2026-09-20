using PiAiAssistant.Domain.Interfaces;
using PiAiAssistant.Infrastructure.Knowledge;

namespace PiAiAssistant.Tests;

public class SopStoreTests
{
    private readonly ISopKnowledgeStore _sops = new InMemorySopStore();

    [Fact]
    public async Task Search_flame_returns_gt04()
    {
        var hits = await _sops.SearchAsync("flame intensity GT01");

        Assert.NotEmpty(hits);
        Assert.Contains(hits, h => h.Id == "SOP-GT-04");
        Assert.Equal("3.2", hits.First(h => h.Id == "SOP-GT-04").Section);
    }

    [Fact]
    public async Task Search_heat_rate_returns_hr12()
    {
        var hits = await _sops.SearchAsync("heat rate fuel");

        Assert.Contains(hits, h => h.Id == "SOP-HR-12");
    }

    [Fact]
    public async Task Search_sector_returns_sec01()
    {
        var hits = await _sops.SearchAsync("sector loading COA");

        Assert.Contains(hits, h => h.Id == "SOP-SEC-01");
    }

    [Fact]
    public async Task Search_respects_max_results()
    {
        var hits = await _sops.SearchAsync("fuel gas turbine heat", maxResults: 2);
        Assert.True(hits.Count <= 2);
    }

    [Fact]
    public async Task Search_unknown_still_returns_fallback_excerpts()
    {
        var hits = await _sops.SearchAsync("zzzz-not-a-real-topic-qqq", maxResults: 2);

        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public async Task GetById_is_case_insensitive()
    {
        var sop = await _sops.GetByIdAsync("sop-gt-04");

        Assert.NotNull(sop);
        Assert.Equal("SOP-GT-04", sop!.Id);
        Assert.Contains("instrumentation", sop.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetById_missing_returns_null()
    {
        Assert.Null(await _sops.GetByIdAsync("SOP-DOES-NOT-EXIST"));
    }

    [Fact]
    public async Task Corpus_includes_spec_sheet()
    {
        var spec = await _sops.GetByIdAsync("SPEC-GT01-FUEL");

        Assert.NotNull(spec);
        Assert.Contains("kg/s", spec!.Body);
    }
}
