using Agent.Tools.Rag;

namespace Agent.Tools.Tests.Rag;

/// <summary>Tests the cosine similarity used to rank policy chunks.</summary>
public sealed class CosineSimilarityTests
{
    [Fact]
    public void Between_IsOneForIdenticalVectors()
    {
        float[] vector = [1f, 2f, 3f];

        Assert.Equal(1d, CosineSimilarity.Between(vector, vector), precision: 10);
    }

    [Fact]
    public void Between_IsZeroForOrthogonalVectors()
    {
        Assert.Equal(0d, CosineSimilarity.Between([1f, 0f], [0f, 1f]), precision: 10);
    }

    [Fact]
    public void Between_IgnoresMagnitude()
    {
        // Same direction, different length: the angle, and therefore the score, is unchanged.
        Assert.Equal(
            1d,
            CosineSimilarity.Between([1f, 1f], [10f, 10f]),
            precision: 10);
    }

    [Fact]
    public void Between_IsZeroWhenAVectorHasNoMagnitude()
    {
        Assert.Equal(0d, CosineSimilarity.Between([0f, 0f], [1f, 1f]));
    }

    [Fact]
    public void Between_RejectsVectorsOfDifferentLengths()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            CosineSimilarity.Between([1f, 2f], [1f]);
        });
    }

    [Fact]
    public void TopK_RanksByDescendingSimilarityAndHonoursTheLimit()
    {
        var query = new float[] { 1f, 0f };

        var best = Chunk("best.md", "Best", [1f, 0f]);
        var middle = Chunk("middle.md", "Middle", [1f, 1f]);
        var worst = Chunk("worst.md", "Worst", [0f, 1f]);

        var results = CosineSimilarity.TopK(query, [worst, best, middle], topK: 2);

        Assert.Equal(2, results.Count);
        Assert.Equal("best.md", results[0].Chunk.SourceFile);
        Assert.Equal("middle.md", results[1].Chunk.SourceFile);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public void TopK_ReturnsNothingWhenTheLimitIsNotPositive()
    {
        Assert.Empty(CosineSimilarity.TopK(new float[] { 1f, 0f }, [Chunk("a.md", "A", [1f, 0f])], topK: 0));
    }

    private static IndexedChunk Chunk(string sourceFile, string heading, float[] vector)
        => new(new PolicyChunk(sourceFile, heading, "1", "text", 0), vector);
}
