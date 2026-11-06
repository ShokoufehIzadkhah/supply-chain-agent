using System.Text.Json;
using Agent.Core;
using Agent.Tools;
using Agent.Tools.Rag;
using Chat.Api.Chat;
using Microsoft.Extensions.Logging.Abstractions;

namespace Chat.Api.Tests;

/// <summary>
/// Tests of what one answer reports about itself. The <c>sources</c> array is derived from the policy
/// searches that ran, not from the text the model wrote, so these tests pin that behaviour down.
/// </summary>
public sealed class ToolCallCollectorTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ToSources_NamesTheFileAndTheSection()
    {
        var collector = new ToolCallCollector();

        await collector.AddAsync(Search(
            Match("order-change-rules.md", "3.2"),
            Match("returns-policy.md", null)));

        Assert.Equal(new[] { "order-change-rules.md §3.2", "returns-policy.md" }, collector.ToSources());
    }

    [Fact]
    public async Task ToSources_DropsARepeatedSourceAcrossSearches()
    {
        var collector = new ToolCallCollector();
        var search = Search(Match("priority-customers.md", "1"));

        await collector.AddAsync(search);
        await collector.AddAsync(search);

        Assert.Equal(new[] { "priority-customers.md §1" }, collector.ToSources());
    }

    [Fact]
    public async Task ToSources_IgnoresToolsThatAreNotPolicySearches()
    {
        var collector = new ToolCallCollector();

        await collector.AddAsync(new ToolCallRecord(
            nameof(OrderTools.GetOrderStatus),
            JsonSerializer.SerializeToElement(new { orderId = 4711 }),
            8.25,
            JsonSerializer.SerializeToElement("{ \"status\": \"Confirmed\" }")));

        Assert.Empty(collector.ToSources());
        Assert.Equal(nameof(OrderTools.GetOrderStatus), Assert.Single(collector.ToToolCalls()).Name);
    }

    [Fact]
    public async Task ToSources_IgnoresASearchThatFoundNothing()
    {
        // A real search over an empty index, so the test does not have to know the message the tool
        // answers with when there is nothing to search.
        var tools = new PolicyTools(
            new FakeEmbeddingGenerator(),
            new InMemoryVectorStore(),
            NullLogger<PolicyTools>.Instance);

        var question = "what is the minimum order quantity for corrugated boxes?";
        var output = await tools.SearchPolicies(question);

        var collector = new ToolCallCollector();
        await collector.AddAsync(new ToolCallRecord(
            nameof(PolicyTools.SearchPolicies),
            JsonSerializer.SerializeToElement(new { question }),
            1.0,
            JsonSerializer.SerializeToElement(output)));

        Assert.Empty(collector.ToSources());
    }

    [Fact]
    public async Task ToToolCalls_ReportsTheNameTheArgumentsAndTheDuration()
    {
        var collector = new ToolCallCollector();

        await collector.AddAsync(new ToolCallRecord(
            nameof(OrderTools.CheckInventory),
            JsonSerializer.SerializeToElement(new { sku = "BOX-500", quantity = 250 }),
            42.5,
            JsonSerializer.SerializeToElement("{ \"available\": 900 }")));

        var call = Assert.Single(collector.ToToolCalls());

        Assert.Equal(nameof(OrderTools.CheckInventory), call.Name);
        Assert.Equal(42.5, call.DurationMs);
        Assert.Equal("BOX-500", call.Args.GetProperty("sku").GetString());
        Assert.Equal(250, call.Args.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task AddAsync_NotifiesTheHostAsEachCallFinishes()
    {
        var reported = new List<string>();
        var collector = new ToolCallCollector((call, _) =>
        {
            reported.Add(call.Name);
            return ValueTask.CompletedTask;
        });

        await collector.AddAsync(Search(Match("delivery-terms.md", "2")));
        await collector.AddAsync(new ToolCallRecord(
            nameof(OrderTools.GetShipmentInfo),
            JsonSerializer.SerializeToElement(new { orderId = 4711 }),
            3.0,
            JsonSerializer.SerializeToElement("{}")));

        Assert.Equal(
            new[] { nameof(PolicyTools.SearchPolicies), nameof(OrderTools.GetShipmentInfo) },
            reported);
    }

    private static PolicySearchMatch Match(string source, string? section)
        => new(source, "Heading", section, 0.9, "The passage that matched.");

    private static ToolCallRecord Search(params PolicySearchMatch[] matches)
    {
        var result = new PolicySearchResult("a question", matches);

        // A tool that returns a string reaches the collector as a JSON string value, because
        // AIFunctionFactory marshals the result before a wrapper sees it.
        return new ToolCallRecord(
            nameof(PolicyTools.SearchPolicies),
            JsonSerializer.SerializeToElement(new { question = "a question" }),
            5.0,
            JsonSerializer.SerializeToElement(JsonSerializer.Serialize(result, Json)));
    }
}
