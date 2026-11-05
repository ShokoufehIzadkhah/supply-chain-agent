using Microsoft.Extensions.AI;

namespace Agent.Tools.Tests.Rag;

/// <summary>
/// Deterministic stand-in for a real embedding model. Every text becomes a fixed-size "bag of words"
/// vector: each token is hashed into one bucket, so two texts score high when they share words and
/// zero when they share none. That is enough to test chunking, ranking and the index without calling
/// a network service or paying for tokens.
/// </summary>
internal sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    /// <summary>Number of buckets in the fake vector.</summary>
    public const int Dimensions = 64;

    /// <inheritdoc />
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var embeddings = values.Select(value => new Embedding<float>(Embed(value))).ToList();
        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private static float[] Embed(string text)
    {
        var vector = new float[Dimensions];

        foreach (var token in text.Split(
                     [' ', '\n', '\r', '\t', '.', ',', ';', ':', '?', '!', '(', ')', '§'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            vector[Bucket(token.ToLowerInvariant())] += 1f;
        }

        return vector;
    }

    /// <summary>Stable FNV-1a hash, so buckets do not move between runs the way <c>string.GetHashCode</c> can.</summary>
    private static int Bucket(string token)
    {
        unchecked
        {
            var hash = 2166136261u;

            foreach (var character in token)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            return (int)(hash % Dimensions);
        }
    }
}
