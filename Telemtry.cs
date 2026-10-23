using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ObservabilityDemo;

public static class Telemetry
{
    public const string Name = "ObservabilityDemo";
    public static readonly ActivitySource Source = new(Name);
    public static readonly Meter Meter = new(Name);

    public static readonly Counter<long> Actions = Meter.CreateCounter<long>("app.actions");
    public static readonly Counter<long> Errors = Meter.CreateCounter<long>("app.errors");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("app.action.duration", "ms");

    // Fuga de memoria simulada (se ve como una metrica que solo sube)
    public static readonly List<byte[]> Leak = new();
    static readonly ObservableGauge<long> LeakGauge =
        Meter.CreateObservableGauge("app.leaked_bytes", () => Leak.Sum(b => (long)b.Length));
}
