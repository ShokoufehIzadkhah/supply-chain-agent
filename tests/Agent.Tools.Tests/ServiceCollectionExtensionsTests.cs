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
    public void AddAgentTools_Throws_WhenBaseUrlMissing()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddAgentTools(Configuration(null)));

        Assert.Contains("ErpApi:BaseUrl", exception.Message);
    }

    [Fact]
    public void AddAgentTools_Throws_WhenBaseUrlIsNotAbsolute()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddAgentTools(Configuration("not-a-url")));

        Assert.Contains("ErpApi:BaseUrl", exception.Message);
    }

    [Fact]
    public void AddAgentTools_Throws_WhenAgentKeyMissing()
    {
        // Every ERP call carries the agent key, so a missing key must fail at startup, not on the
        // first question.
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddAgentTools(Configuration("http://erp.test:8080", agentKey: null)));

        Assert.Contains("ApiKeys:Agent", exception.Message);
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
