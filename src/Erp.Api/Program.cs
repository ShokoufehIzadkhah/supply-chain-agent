using System.Reflection;
using System.Text.Json.Serialization;
using Erp.Api.Authentication;
using Erp.Api.Data;
using Erp.Api.Data.Seed;
using Erp.Api.Features.ChangeRequests;
using Erp.Api.Features.Inventory;
using Erp.Api.Features.Orders;
using Erp.Api.Features.Shipments;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Surface the XML documentation comments (summaries, parameter and response descriptions).
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});

// Enums (order and shipment status) are emitted as their names rather than numbers so the
// responses stay readable for both humans and the agent that consumes this API.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<ErpDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ErpDb")));

// API-key authentication: the agent key can propose and read change requests, the supervisor key
// can read and decide. The keys are supplied through user-secrets or environment variables.
builder.Services.AddApiKeyAuthentication();

var app = builder.Build();

// In development the local SQLite database is created/upgraded and filled with the
// deterministic demo dataset the first time it is empty.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
    await db.Database.MigrateAsync();
    await ErpDataSeeder.SeedAsync(db);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapOrderEndpoints();
app.MapInventoryEndpoints();
app.MapShipmentEndpoints();
app.MapChangeRequestEndpoints();

app.Run();

// Exposed so the integration tests can boot the API with WebApplicationFactory<Program>.
public partial class Program;
