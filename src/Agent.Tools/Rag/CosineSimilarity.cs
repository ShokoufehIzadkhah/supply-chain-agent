namespace Agent.Tools.Rag;

/// <summary>
/// Cosine similarity between two embedding vectors, plus the ranking helper the vector stores share.
/// Only the angle between the vectors matters, so the result is independent of their magnitude and a
/// score of 1 means identical direction, 0 means unrelated.
/// </summary>
public static class CosineSimilarity
{
    /// <summary>Computes the cosine similarity of two vectors of the same length.</summary>
    /// <param name="left">First vector.</param>
    /// <param name="right">Second vector; must have the same length as <paramref name="left"/>.</param>
    /// <returns>A value in the range -1 to 1. Zero when either vector has no magnitude.</returns>
    /// <exception cref="ArgumentException">The vectors have different lengths.</exception>
    public static double Between(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException(
                $"Embedding vectors must have the same length, but were {left.Length} and {right.Length}.",
                nameof(right));
        }

        double dot = 0;
        double leftSquares = 0;
        double rightSquares = 0;

        for (var i = 0; i < left.Length; i++)
        {
            dot += (double)left[i] * right[i];
            leftSquares += (double)left[i] * left[i];
            rightSquares += (double)right[i] * right[i];
        }

        if (leftSquares == 0 || rightSquares == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftSquares) * Math.Sqrt(rightSquares));
    }

    /// <summary>Ranks chunks against a query vector and returns the best <paramref name="topK"/>.</summary>
    /// <param name="query">Embedded question.</param>
    /// <param name="chunks">Every indexed chunk to rank.</param>
    /// <param name="topK">Maximum number of results to return.</param>
    /// <returns>The matches ordered by descending similarity.</returns>
    public static IReadOnlyList<VectorSearchResult> TopK(
        ReadOnlyMemory<float> query,
        IEnumerable<IndexedChunk> chunks,
        int topK)
    {
        if (topK <= 0)
        {
            return [];
        }

        return
        [
            .. chunks
                .Select(chunk => new VectorSearchResult(
                    chunk.Chunk,
                    Between(query.Span, chunk.Vector.Span)))
                .OrderByDescending(result => result.Score)
                .Take(topK),
        ];
    }
}
