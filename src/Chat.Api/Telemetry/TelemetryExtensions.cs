using Agent.Core;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Chat.Api.Telemetry;

/// <summary>
/// Wires OpenTelemetry into the chat host. Traces and metrics always go to the console, so a local run
/// shows what the agent did with no setup at all; when <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> is
/// set they also go to Azure Application Insights.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>Service name reported to every exporter.</summary>
    private const string ServiceName = "chat-api";

    /// <summary>
    /// Environment variable holding the Application Insights connection string. The default host
    /// configuration already reads environment variables, so it is read through configuration here.
    /// </summary>
    private const string ApplicationInsightsKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    /// <summary>How often the console exporter prints the metrics it has collected.</summary>
    private const int ConsoleMetricExportIntervalMs = 10_000;

    /// <summary>
    /// Registers the tracing and metrics pipeline for Chat.Api: it subscribes to the agent's
    /// <see cref="AgentTelemetry.Source"/> and <see cref="AgentTelemetry.Meter"/> and exports them to the
    /// console, plus Application Insights when a connection string is configured.
    /// </summary>
    /// <param name="builder">The web application builder to register the pipeline on.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static WebApplicationBuilder AddChatTelemetry(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration[ApplicationInsightsKey];
        var useApplicationInsights = !string.IsNullOrWhiteSpace(connectionString);

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(AgentTelemetry.SourceName)
                    .AddConsoleExporter();

                if (useApplicationInsights)
                {
                    tracing.AddAzureMonitorTraceExporter(options => options.ConnectionString = connectionString);
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(AgentTelemetry.MeterName)
                    .AddConsoleExporter((_, readerOptions) =>
                        readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
                            ConsoleMetricExportIntervalMs);

                if (useApplicationInsights)
                {
                    metrics.AddAzureMonitorMetricExporter(options => options.ConnectionString = connectionString);
                }
            });

        return builder;
    }
}
