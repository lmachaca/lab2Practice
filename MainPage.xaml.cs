using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ObservabilityDemo;

public partial class MainPage : ContentPage
{
    readonly ILogger<MainPage> _log;

    public MainPage(ILogger<MainPage> log)
    {
        InitializeComponent();
        _log = log;
    }

    async void OnLoadClicked(object? s, EventArgs e) => await LoadItemsAsync();

    async void OnBurstClicked(object? s, EventArgs e)
    {
        for (int i = 0; i < 20; i++) await LoadItemsAsync();
    }

    async Task LoadItemsAsync()
    {
        var sw = Stopwatch.StartNew();
        using var act = Telemetry.Source.StartActivity("load_items");
        act?.SetTag("fault.slow", SlowSwitch.IsToggled);
        act?.SetTag("fault.nplusone", NPlusOneSwitch.IsToggled);
        try
        {
            // fix 3: DEPENDENCIA LENTA -> CACHE =====
            // before:
            await GetItemsAsync();
            //after:
            // await GetItemsCachedAsync();

            if (LeakSwitch.IsToggled)
            {
                Telemetry.Leak.Add(new byte[5 * 1024 * 1024]);

                // fix 4: FUGA DE MEMORIA -> LIMITAR LO QUE SE GUARDA 
                // after:
                // while (Telemetry.Leak.Count > 3) Telemetry.Leak.RemoveAt(0);
            }

            _log.LogInformation("Items loaded in {ElapsedMs} ms", sw.ElapsedMilliseconds);
            ResultLabel.Text = $"OK en {sw.ElapsedMilliseconds} ms";
        }
        catch (Exception ex)
        {
            act?.SetStatus(ActivityStatusCode.Error, ex.Message);
            act?.AddException(ex);
            Telemetry.Errors.Add(1);
            _log.LogError(ex, "Failed to load items");
            ResultLabel.Text = $"ERROR: {ex.Message}";
        }
        finally
        {
            Telemetry.Actions.Add(1);
            Telemetry.Duration.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    //fix 3: CACHE 
    // static bool _cached;
    // static DateTime _cacheTime;
    //
    // async Task GetItemsCachedAsync()
    // {
    //     bool hit = _cached && DateTime.UtcNow - _cacheTime < TimeSpan.FromSeconds(30);
    //     Activity.Current?.SetTag("cache.hit", hit);
    //     if (hit) return;                 // sin tocar la base de datos
    //     await GetItemsAsync();
    //     _cached = true;
    //     _cacheTime = DateTime.UtcNow;
    // }

    // fix 2: ERROR INTERMITENTE -> REINTENTOS =====
    async Task GetItemsAsync()
    {
        // before:
        await FetchAsync();
        // after:
        // await RetryAsync(FetchAsync, 3);
    }

    // fix 2:
    // async Task RetryAsync(Func<Task> op, int attempts)
    // {
    //     for (int i = 1; ; i++)
    //     {
    //         try { await op(); return; }
    //         catch (InvalidOperationException ex) when (i < attempts)
    //         {
    //             _log.LogWarning(ex, "Attempt {Attempt} failed, retrying", i);
    //             await Task.Delay(100 * i);
    //         }
    //     }
    // }

    async Task FetchAsync()
    {
        // fx 1: N+1 -> UNA SOLA CONSULTA =====
        // before:
        if (NPlusOneSwitch.IsToggled)
            for (int i = 0; i < 10; i++) await QueryAsync($"item {i}");
        else
            await QueryAsync("all items");
        // after (comenta las 4 lineas de arriba y descomenta esta):
        // await QueryAsync("all items");

        // Falla de error intermitente (esto simula la dependencia, no se toca)
        if (ErrorSwitch.IsToggled && Random.Shared.NextDouble() < 0.5)
            throw new InvalidOperationException("Simulated DB timeout");
    }

    async Task QueryAsync(string what)
    {
        using var span = Telemetry.Source.StartActivity("db.query");
        span?.SetTag("db.statement", $"SELECT {what}");
        await Task.Delay(SlowSwitch.IsToggled ? 300 : 30);
    }
}