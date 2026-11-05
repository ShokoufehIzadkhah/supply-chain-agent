using Agent.Tools.Rag;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agent.Tools.Tests.Rag;

/// <summary>
/// Tests the incremental index: a file is embedded once, and only re-embedded when its content
/// changes or when it disappears from the policy directory.
/// </summary>
public sealed class PolicyIndexerTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"policy-index-{Guid.NewGuid():N}");

    public PolicyIndexerTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Rebuild_IndexesNewFilesAndSkipsUnchangedOnes()
    {
        await WritePolicyAsync("delivery-terms.md", Document("lead time"));
        var store = new InMemoryVectorStore();
        var indexer = CreateIndexer(store);

        var first = await indexer.RebuildAsync(_directory);

        Assert.Equal("delivery-terms.md", Assert.Single(first.Indexed));
        Assert.Empty(first.Skipped);
        Assert.True(first.Chunks > 0);
        Assert.Equal("delivery-terms.md", Assert.Single(await store.ListSourcesAsync()));

        var second = await indexer.RebuildAsync(_directory);

        Assert.Equal("delivery-terms.md", Assert.Single(second.Skipped));
        Assert.Empty(second.Indexed);
        Assert.Equal(0, second.Chunks);
    }

    [Fact]
    public async Task Rebuild_ReindexesAFileWhoseContentChanged()
    {
        var path = await WritePolicyAsync("returns-policy.md", Document("30 calendar days"));
        var store = new InMemoryVectorStore();
        var indexer = CreateIndexer(store);

        await indexer.RebuildAsync(_directory);
        await File.WriteAllTextAsync(path, Document("60 calendar days"));

        var report = await indexer.RebuildAsync(_directory);

        Assert.Equal("returns-policy.md", Assert.Single(report.Indexed));
        Assert.Empty(report.Skipped);
    }

    [Fact]
    public async Task Rebuild_DropsSourcesWhoseFileDisappeared()
    {
        var path = await WritePolicyAsync("priority-customers.md", Document("priority"));
        var store = new InMemoryVectorStore();
        var indexer = CreateIndexer(store);

        await indexer.RebuildAsync(_directory);
        File.Delete(path);

        var report = await indexer.RebuildAsync(_directory);

        Assert.Equal("priority-customers.md", Assert.Single(report.Removed));
        Assert.Empty(await store.ListSourcesAsync());
    }

    [Fact]
    public async Task Rebuild_SucceedsWhenTheDirectoryDoesNotExist()
    {
        var indexer = CreateIndexer(new InMemoryVectorStore());

        var report = await indexer.RebuildAsync(Path.Combine(_directory, "missing"));

        Assert.Empty(report.Indexed);
        Assert.Equal(0, report.Chunks);
    }

    private PolicyIndexer CreateIndexer(IVectorStore store)
        => new(new FakeEmbeddingGenerator(), store, NullLogger<PolicyIndexer>.Instance);

    private async Task<string> WritePolicyAsync(string fileName, string markdown)
    {
        var path = Path.Combine(_directory, fileName);
        await File.WriteAllTextAsync(path, markdown);
        return path;
    }

    private static string Document(string marker)
        => $"# Policy\n\n## 1. Rule\n\n{string.Join(' ', Enumerable.Repeat(marker, 20))}";
}
