using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;

namespace ObservabilityDemo;

public static class MauiProgram
{
    //add the local connection string for Azure Application Insights here if you want to use it
    const string AppInsightsConnectionString = "";

    static TracerProvider? _tracer;
    static MeterProvider? _meters;

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // for android emulatro
        var endpoint = new Uri(DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:4317" : "http://localhost:4317");
        var resource = ResourceBuilder.CreateDefault().AddService("observability-demo");

        // Trazas
        var tb = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .AddSource(Telemetry.Name)
            .AddOtlpExporter(o => o.Endpoint = endpoint);
        if (!string.IsNullOrEmpty(AppInsightsConnectionString))
            tb.AddAzureMonitorTraceExporter(o => o.ConnectionString = AppInsightsConnectionString);
        _tracer = tb.Build();

        // Metricas
        var mb = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resource)
            .AddMeter(Telemetry.Name)
            .AddOtlpExporter((o, reader) =>
            {
                o.Endpoint = endpoint;
                reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000;
            });
        if (!string.IsNullOrEmpty(AppInsightsConnectionString))
            mb.AddAzureMonitorMetricExporter(o => o.ConnectionString = AppInsightsConnectionString);
        _meters = mb.Build();

        // Logs
        builder.Logging.AddOpenTelemetry(o =>
        {
            o.SetResourceBuilder(resource);
            o.IncludeScopes = true;
            o.AddOtlpExporter(x => x.Endpoint = endpoint);
            if (!string.IsNullOrEmpty(AppInsightsConnectionString))
                o.AddAzureMonitorLogExporter(x => x.ConnectionString = AppInsightsConnectionString);
        });

        builder.Services.AddTransient<MainPage>();
        return builder.Build();
    }
}