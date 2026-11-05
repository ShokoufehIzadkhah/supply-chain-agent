using Agent.Tools.Rag;
using Microsoft.Data.Sqlite;

namespace Agent.Tools.Tests.Rag;

/// <summary>
/// Tests the SQLite store the console application uses by default: it must round-trip the chunk text,
/// the source file, the heading, the section and the vector, and replace the chunks of a file.
/// </summary>
public sealed class SqliteVectorStoreTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"policy-index-{Guid.NewGuid():N}",
        "policy-index.db");

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools connections, so the file stays locked until the pool is cleared.
        SqliteConnection.ClearAllPools();

        var directory = Path.GetDirectoryName(_databasePath);

        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Store_ThenSearch_RoundTripsTheChunkAndItsVector()
    {
        var store = new SqliteVectorStore(_databasePath);
        await store.InitializeAsync();

        var chunk = new PolicyChunk(
            "order-change-rules.md",
            "Order Change Rules > 2. The five-day rule",
            "2",
            "A delivery date change is allowed until five days before the production start date.",
            0);

        await store.StoreAsync(
            "order-change-rules.md",
            "hash-1",
            [new IndexedChunk(chunk, new float[] { 1f, 0f, 0f })]);

        Assert.Equal("hash-1", await store.GetContentHashAsync("order-change-rules.md"));

        var result = Assert.Single(await store.SearchAsync(new float[] { 1f, 0f, 0f }, topK: 3));

        Assert.Equal("order-change-rules.md", result.Chunk.SourceFile);
        Assert.Equal("Order Change Rules > 2. The five-day rule", result.Chunk.Heading);
        Assert.Equal("2", result.Chunk.Section);
        Assert.Equal(0, result.Chunk.ChunkIndex);
        Assert.Contains("five days before", result.Chunk.Text);
        Assert.Equal(1d, result.Score, precision: 6);
    }

    [Fact]
    public async Task Store_ReplacesThePreviousChunksAndDeleteRemovesTheSource()
    {
        var store = new SqliteVectorStore(_databasePath);
        await store.InitializeAsync();

        var old = new PolicyChunk("returns-policy.md", "Returns Policy", null, "30 calendar days", 0);
        var updated = new PolicyChunk("returns-policy.md", "Returns Policy", null, "60 calendar days", 0);

        await store.StoreAsync("returns-policy.md", "hash-1", [new IndexedChunk(old, new float[] { 1f })]);
        await store.StoreAsync("returns-policy.md", "hash-2", [new IndexedChunk(updated, new float[] { 1f })]);

        var result = Assert.Single(await store.SearchAsync(new float[] { 1f }, topK: 5));

        Assert.Equal("60 calendar days", result.Chunk.Text);
        Assert.Equal("hash-2", await store.GetContentHashAsync("returns-policy.md"));

        await store.DeleteAsync("returns-policy.md");

        Assert.Empty(await store.SearchAsync(new float[] { 1f }, topK: 5));
        Assert.Empty(await store.ListSourcesAsync());
    }
}
