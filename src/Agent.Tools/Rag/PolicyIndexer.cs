using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Tools.Rag;

/// <summary>What one <see cref="PolicyIndexer.RebuildAsync"/> pass did.</summary>
/// <param name="Indexed">Files that were chunked and embedded again.</param>
/// <param name="Skipped">Files whose hash was unchanged, so their vectors were reused.</param>
/// <param name="Removed">Files that disappeared and had their vectors dropped.</param>
/// <param name="Chunks">Number of chunks written for the indexed files.</param>
public sealed record IndexReport(
    IReadOnlyList<string> Indexed,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> Removed,
    int Chunks);

/// <summary>
/// Keeps the policy vector index in step with the Markdown documents on disk. Each file is hashed, and
/// only files whose hash changed are chunked and embedded again, so restarting the application costs
/// nothing when the policies did not move.
/// </summary>
public sealed class PolicyIndexer
{
    private const string FilePattern = "*.md";

    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddings;
    private readonly IVectorStore _store;
    private readonly ILogger<PolicyIndexer> _logger;

    /// <summary>Creates the indexer.</summary>
    /// <param name="embeddings">Embedding generator, from the configured <c>AI:EmbeddingModel</c>.</param>
    /// <param name="store">Where the chunks and their vectors are kept.</param>
    /// <param name="logger">Logger that records what each pass indexed or skipped.</param>
    public PolicyIndexer(
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        IVectorStore store,
        ILogger<PolicyIndexer> logger)
    {
        _embeddings = embeddings;
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Re-indexes every Markdown document in <paramref name="policyDirectory"/>, touching only the
    /// files whose content changed since the last pass.
    /// </summary>
    /// <param name="policyDirectory">Directory holding the policy documents.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A summary of the pass.</returns>
    public async Task<IndexReport> RebuildAsync(
        string policyDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyDirectory);

        await _store.InitializeAsync(cancellationToken);

        var indexed = new List<string>();
        var skipped = new List<string>();
        var removed = new List<string>();
        var chunkCount = 0;

        var files = ReadFiles(policyDirectory);

        foreach (var file in files)
        {
            var sourceFile = Path.GetFileName(file);
            var markdown = await File.ReadAllTextAsync(file, cancellationToken);
            var hash = ComputeHash(markdown);

            // The hash is the whole point of the incremental index: identical content means the stored
            // vectors are still correct, so the expensive embedding call is skipped entirely.
            var storedHash = await _store.GetContentHashAsync(sourceFile, cancellationToken);
            if (string.Equals(storedHash, hash, StringComparison.Ordinal))
            {
                skipped.Add(sourceFile);
                continue;
            }

            var chunks = PolicyChunker.Chunk(markdown, sourceFile);
            var indexedChunks = await EmbedAsync(chunks, cancellationToken);

            await _store.StoreAsync(sourceFile, hash, indexedChunks, cancellationToken);

            indexed.Add(sourceFile);
            chunkCount += indexedChunks.Count;
        }

        foreach (var source in await _store.ListSourcesAsync(cancellationToken))
        {
            // A policy document that was renamed or deleted must not keep answering questions.
            if (!files.Any(file => string.Equals(Path.GetFileName(file), source, StringComparison.OrdinalIgnoreCase)))
            {
                await _store.DeleteAsync(source, cancellationToken);
                removed.Add(source);
            }
        }

        _logger.LogInformation(
            "Policy index rebuilt: {IndexedCount} indexed, {SkippedCount} unchanged, {RemovedCount} removed, " +
            "{ChunkCount} chunks stored",
            indexed.Count,
            skipped.Count,
            removed.Count,
            chunkCount);

        return new IndexReport(indexed, skipped, removed, chunkCount);
    }

    private static IReadOnlyList<string> ReadFiles(string policyDirectory)
    {
        if (!Directory.Exists(policyDirectory))
        {
            return [];
        }

        return
        [
            .. Directory
                .EnumerateFiles(policyDirectory, FilePattern, SearchOption.TopDirectoryOnly)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase),
        ];
    }

    private async Task<IReadOnlyList<IndexedChunk>> EmbedAsync(
        IReadOnlyList<PolicyChunk> chunks,
        CancellationToken cancellationToken)
    {
        if (chunks.Count == 0)
        {
            return [];
        }

        // Embed the heading together with the body: headings are short and carry the section title,
        // which measurably improves matching for questions phrased in the document's own words.
        var texts = chunks.Select(chunk => $"{chunk.Heading}\n\n{chunk.Text}").ToList();
        var embeddings = await _embeddings.GenerateAsync(texts, cancellationToken: cancellationToken);

        return [.. chunks.Select((chunk, index) => new IndexedChunk(chunk, embeddings[index].Vector))];
    }

    private static string ComputeHash(string content)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
