namespace Agent.Tools.Rag;

/// <summary>
/// Vector store that keeps everything in memory. It ranks with a brute-force cosine scan, which is
/// more than fast enough for a handful of policy documents and makes the behaviour deterministic and
/// easy to test. The application uses <see cref="SqliteVectorStore"/> instead; the two are
/// interchangeable because both implement <see cref="IVectorStore"/>.
/// </summary>
public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly Dictionary<string, SourceEntry> _sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListSourcesAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<string>>([.. _sources.Keys]);
        }
    }

    /// <inheritdoc />
    public Task<string?> GetContentHashAsync(string sourceFile, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_sources.TryGetValue(sourceFile, out var entry) ? entry.ContentHash : null);
        }
    }

    /// <inheritdoc />
    public Task StoreAsync(
        string sourceFile,
        string contentHash,
        IReadOnlyList<IndexedChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _sources[sourceFile] = new SourceEntry(contentHash, [.. chunks]);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string sourceFile, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _sources.Remove(sourceFile);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> query,
        int topK,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(
                CosineSimilarity.TopK(query, _sources.Values.SelectMany(entry => entry.Chunks), topK));
        }
    }

    private sealed record SourceEntry(string ContentHash, IReadOnlyList<IndexedChunk> Chunks);
}
