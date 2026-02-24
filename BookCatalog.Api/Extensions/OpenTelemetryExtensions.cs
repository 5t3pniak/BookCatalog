using System.Reflection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BookCatalog.Api.Extensions;

internal static class OpenTelemetryExtensions
{
    private const string ServiceName = "BookCatalog.Api";
    
    internal static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName: ServiceName, serviceVersion: version)
                .AddTelemetrySdk()
                .AddEnvironmentVariableDetector())
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter();

                if (builder.Environment.IsDevelopment())
                    metrics.AddConsoleExporter();
            });

        return builder;
    }
}
