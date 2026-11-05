using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;

namespace PulseFit.Api.Jobs;

public record JobInfo(string Name, string Description, JobRun? LastRun);

/// <summary>
/// Runs jobs, each in its own scope, and records every run for the Jobs screen. Jobs run in the order
/// they're registered (see Program.cs): billing first, then the status changes that follow from it.
/// </summary>
public class JobRunner
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<JobRunner> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JobRunner(IServiceScopeFactory scopes, ILogger<JobRunner> log) { _scopes = scopes; _log = log; }

    public async Task<List<JobInfo>> ListAsync()
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymDbContext>();
        var result = new List<JobInfo>();
        foreach (var job in scope.ServiceProvider.GetServices<IGymJob>())
        {
            var last = await db.JobRuns.Where(r => r.Job == job.Name).OrderByDescending(r => r.Id).FirstOrDefaultAsync();
            result.Add(new JobInfo(job.Name, job.Description, last));
        }
        return result;
    }

    public async Task RunAllAsync(CancellationToken ct)
    {
        foreach (var job in await ListAsync()) await RunAsync(job.Name, ct);
    }

    public async Task<JobRun> RunAsync(string name, CancellationToken ct)
    {
        // One job at a time: they change the same members.
        await _gate.WaitAsync(ct);
        try
        {
            using var scope = _scopes.CreateScope();
            var job = scope.ServiceProvider.GetServices<IGymJob>().FirstOrDefault(j => j.Name == name)
                ?? throw new KeyNotFoundException($"No job called {name}.");
            var db = scope.ServiceProvider.GetRequiredService<GymDbContext>();
            var run = new JobRun { Job = job.Name, StartedAt = DateTime.UtcNow };
            try
            {
                run.Summary = await job.RunAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Job {Job} failed", job.Name);
                db.ChangeTracker.Clear();
                run.Failed = true;
                run.Summary = ex.Message;
            }
            run.FinishedAt = DateTime.UtcNow;
            db.JobRuns.Add(run);
            await db.SaveChangesAsync(CancellationToken.None);
            return run;
        }
        finally { _gate.Release(); }
    }
}

/// <summary>Runs every job on a fixed interval: every 15 minutes by default, so the demo gym's day moves along.</summary>
public class JobScheduler : BackgroundService
{
    private readonly JobRunner _runner;
    private readonly TimeSpan _interval;

    public JobScheduler(JobRunner runner, IConfiguration config)
    {
        _runner = runner;
        _interval = TimeSpan.FromMinutes(config.GetValue("Jobs:IntervalMinutes", 15));
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);
        do { await _runner.RunAllAsync(ct); }
        while (await timer.WaitForNextTickAsync(ct));
    }
}
