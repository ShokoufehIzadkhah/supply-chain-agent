using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Tools.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddAgentTools_UsesConfiguredBaseUrlAndAgentKey_ForToolRequests()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAgentTools(Configuration("http://erp.test:8080"));
        services.AddHttpClient("ErpApiClient").ConfigurePrimaryHttpMessageHandler(() => handler);

        using var provider = services.BuildServiceProvider();
        var tools = provider.GetRequiredService<OrderTools>();

        var result = await tools.GetOrderStatus(1023);

        Assert.Equal("Order 1023 was not found in the ERP.", result);
        Assert.Equal("http://erp.test:8080/orders/1023", handler.Requests.Single().RequestUri!.ToString());
        Assert.Equal("test-agent-key", handler.LastRequestApiKey);
    }

    [Fact]
    public void ResolvingOrderTools_Throws_WhenBaseUrlMissing()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ResolveOrderTools(Configuration(null)));

        Assert.Contains("ErpApi:BaseUrl", exception.Message);
    }

    [Fact]
    public void ResolvingOrderTools_Throws_WhenBaseUrlIsNotAbsolute()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ResolveOrderTools(Configuration("not-a-url")));

        Assert.Contains("ErpApi:BaseUrl", exception.Message);
    }

    [Fact]
    public void ResolvingOrderTools_Throws_WhenAgentKeyMissing()
    {
        // Every ERP call carries the agent key, so a missing key must fail as soon as a client is
        // built, not surface later as a 401 on the first question.
        var exception = Assert.Throws<InvalidOperationException>(
            () => ResolveOrderTools(Configuration("http://erp.test:8080", agentKey: null)));

        Assert.Contains("ApiKeys:Agent", exception.Message);
    }

    /// <summary>
    /// Registers the tools and forces a client to be built. The base URL and the agent key are read
    /// then — not when <c>AddAgentTools</c> runs — so a host can merge configuration sources in between.
    /// </summary>
    private static void ResolveOrderTools(IConfiguration configuration)
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddAgentTools(configuration)
            .BuildServiceProvider();

        _ = provider.GetRequiredService<OrderTools>();
    }

    private static IConfiguration Configuration(string? baseUrl, string? agentKey = "test-agent-key")
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ErpApi:BaseUrl"] = baseUrl,
                ["ApiKeys:Agent"] = agentKey
            })
            .Build();
}
