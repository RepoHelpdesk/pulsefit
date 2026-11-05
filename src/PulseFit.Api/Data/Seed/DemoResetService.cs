using PulseFit.Api.Services;

namespace PulseFit.Api.Data.Seed;

/// <summary>
/// Seeds the database on startup and, when <c>Demo:NightlyReset</c> is on, puts the gym back to its
/// starting point every night at 3am gym time, so every demo starts from the same stories.
/// </summary>
public class DemoResetService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly GymClock _clock;
    private readonly ILogger<DemoResetService> _log;

    public DemoResetService(IServiceScopeFactory scopes, IConfiguration config, GymClock clock, ILogger<DemoResetService> log)
    {
        _scopes = scopes; _config = config; _clock = clock; _log = log;
    }

    /// <summary>Run before the app takes requests: a fresh database on every start when <c>Demo:ResetOnStartup</c> is on.</summary>
    public static async Task PrepareAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
        if (config.GetValue("Demo:ResetOnStartup", false)) await seeder.ResetAsync();
        else await seeder.EnsureSeededAsync();
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_config.GetValue("Demo:NightlyReset", false)) return;
        while (!ct.IsCancellationRequested)
        {
            var now = _clock.LocalNow;
            var next = now.Date.AddHours(3);
            if (next <= now) next = next.AddDays(1);
            await Task.Delay(next - now, ct);

            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DemoSeeder>().ResetAsync();
            _log.LogInformation("Demo data reset");
        }
    }
}
