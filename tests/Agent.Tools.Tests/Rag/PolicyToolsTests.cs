using System.Text.Json;
using Agent.Tools.Rag;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agent.Tools.Tests.Rag;

/// <summary>Tests the <c>SearchPolicies</c> tool: ranking, the top-three limit and the citation fields.</summary>
public sealed class PolicyToolsTests
{
    [Fact]
    public async Task SearchPolicies_ReturnsTheTopThreeChunksWithTheirSourceAndHeading()
    {
        var tools = new PolicyTools(
            new FakeEmbeddingGenerator(),
            await PopulateAsync(),
            NullLogger<PolicyTools>.Instance);

        var output = await tools.SearchPolicies(
            "how many days before production start can the delivery date change?");

        using var json = JsonDocument.Parse(output);
        var matches = json.RootElement.GetProperty("matches").EnumerateArray().ToList();

        Assert.Equal(3, matches.Count);

        var best = matches[0];
        Assert.Equal("order-change-rules.md", best.GetProperty("source").GetString());
        Assert.Equal("2", best.GetProperty("section").GetString());
        Assert.Contains("five-day rule", best.GetProperty("heading").GetString());
        Assert.Contains("five days before the production start", best.GetProperty("text").GetString());
        Assert.True(best.GetProperty("score").GetDouble() > matches[1].GetProperty("score").GetDouble());
    }

    [Fact]
    public async Task SearchPolicies_ReturnsAChunkForEverySourceDocumentItSearches()
    {
        var tools = new PolicyTools(
            new FakeEmbeddingGenerator(),
            await PopulateAsync(),
            NullLogger<PolicyTools>.Instance);

        var output = await tools.SearchPolicies("what is the minimum order quantity for corrugated boxes?");

        using var json = JsonDocument.Parse(output);
        var best = json.RootElement.GetProperty("matches")[0];

        Assert.Equal("minimum-order-quantities.md", best.GetProperty("source").GetString());
        Assert.Contains("500 pieces", best.GetProperty("text").GetString());
    }

    [Fact]
    public async Task SearchPolicies_SaysSoWhenNothingIsIndexed()
    {
        var tools = new PolicyTools(
            new FakeEmbeddingGenerator(),
            new InMemoryVectorStore(),
            NullLogger<PolicyTools>.Instance);

        var output = await tools.SearchPolicies("what is the return window?");

        Assert.Contains("No policy documents are indexed", output);
    }

    private static async Task<IVectorStore> PopulateAsync()
    {
        var store = new InMemoryVectorStore();
        var embeddings = new FakeEmbeddingGenerator();

        PolicyChunk[] chunks =
        [
            new("order-change-rules.md", "Order Change Rules > 2. The five-day rule", "2",
                "A delivery date change is allowed until five days before the production start date.", 0),
            new("order-change-rules.md", "Order Change Rules > 3. Postponement", "3",
                "A postponement of at most fourteen calendar days is allowed.", 1),
            new("returns-policy.md", "Returns Policy > 2. Return window", "2",
                "Returns are accepted within 30 calendar days of delivery.", 2),
            new("minimum-order-quantities.md", "Minimum Order Quantities > 2. Quantities", "2",
                "The minimum order quantity for corrugated boxes is 500 pieces.", 3),
        ];

        foreach (var group in chunks.GroupBy(chunk => chunk.SourceFile))
        {
            var indexed = new List<IndexedChunk>();

            foreach (var chunk in group)
            {
                var generated = await embeddings.GenerateAsync([$"{chunk.Heading}\n\n{chunk.Text}"]);
                indexed.Add(new IndexedChunk(chunk, generated[0].Vector));
            }

            await store.StoreAsync(group.Key, $"hash-{group.Key}", indexed);
        }

        return store;
    }
}
