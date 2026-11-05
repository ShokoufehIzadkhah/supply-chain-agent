namespace Agent.Tools.Rag;

/// <summary>A chunk together with its embedding vector, as written to a vector store.</summary>
/// <param name="Chunk">The chunk metadata and text.</param>
/// <param name="Vector">Embedding of the chunk.</param>
public sealed record IndexedChunk(PolicyChunk Chunk, ReadOnlyMemory<float> Vector);

/// <summary>One search hit: a stored chunk and how close it is to the question.</summary>
/// <param name="Chunk">The matching chunk.</param>
/// <param name="Score">Cosine similarity to the query, where 1 is a perfect match.</param>
public sealed record VectorSearchResult(PolicyChunk Chunk, double Score);

/// <summary>
/// Storage for policy chunks and their embeddings. The only implementation detail callers rely on is
/// cosine similarity ranking, so a hosted index such as Azure AI Search can replace the SQLite store
/// without touching the indexer, the tools or the tests.
/// </summary>
public interface IVectorStore
{
    /// <summary>Creates the store if it does not exist yet. Safe to call more than once.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists every source file currently stored.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The stored file names, for example <c>returns-policy.md</c>.</returns>
    Task<IReadOnlyList<string>> ListSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads the content hash recorded for one source file.</summary>
    /// <param name="sourceFile">File name to look up.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The hash last stored for the file, or <c>null</c> when the file is not indexed.</returns>
    Task<string?> GetContentHashAsync(string sourceFile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces every chunk of one source file. Used to re-index a file whose content changed.
    /// </summary>
    /// <param name="sourceFile">File name the chunks belong to.</param>
    /// <param name="contentHash">Hash of the file content, used to skip unchanged files next time.</param>
    /// <param name="chunks">All chunks of the file, in document order. May be empty.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task StoreAsync(
        string sourceFile,
        string contentHash,
        IReadOnlyList<IndexedChunk> chunks,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a source file and all of its chunks.</summary>
    /// <param name="sourceFile">File name to remove.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task DeleteAsync(string sourceFile, CancellationToken cancellationToken = default);

    /// <summary>Returns the chunks most similar to the query vector.</summary>
    /// <param name="query">Embedded question.</param>
    /// <param name="topK">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The top matches ordered by descending cosine similarity.</returns>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> query,
        int topK,
        CancellationToken cancellationToken = default);
}
