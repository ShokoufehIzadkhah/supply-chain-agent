using Erp.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Erp.Api.Tests;

/// <summary>
/// Boots the API in-process against a private SQLite in-memory database. Every factory instance
/// gets its own database, so test classes cannot see each other's data, and that database is
/// migrated and seeded by the very same startup code the real app uses.
/// </summary>
public sealed class ErpApiFactory : WebApplicationFactory<Program>
{
    // Keys the tests authenticate with. They are injected through configuration so no developer
    // secret is needed to run the suite.
    public const string AgentApiKey = "test-agent-key";
    public const string SupervisorApiKey = "test-supervisor-key";

    // A SQLite in-memory database only lives as long as at least one connection to it is open,
    // so this connection is held for the lifetime of the factory (i.e. the whole test class).
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development is what makes Program.cs migrate the database and run the deterministic seeder.
        builder.UseEnvironment("Development");

        _connection.Open();

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKeys:Agent"] = AgentApiKey,
                ["ApiKeys:Supervisor"] = SupervisorApiKey
            }));

        builder.ConfigureServices(services =>
        {
            // Replace the file-based SQLite context from Program.cs with one bound to the in-memory
            // connection. Both registrations must be removed: since EF Core 9 the options are built
            // from every IDbContextOptionsConfiguration, so leaving the original in place would
            // register two database providers and throw at runtime.
            services.RemoveAll<DbContextOptions<ErpDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ErpDbContext>>();

            services.AddDbContext<ErpDbContext>(options => options.UseSqlite(_connection));
        });
    }

    /// <summary>Runs a query against the same database the API under test is using.</summary>
    public async Task<T> QueryAsync<T>(Func<ErpDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<ErpDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
