namespace PulseFit.Api.Jobs;

/// <summary>
/// A background job. Each runs on a schedule (see <see cref="JobScheduler"/>) and can be run by hand
/// from the Jobs screen. A job must be safe to run twice: it only acts on what is due.
/// </summary>
public interface IGymJob
{
    /// <summary>Short id, also the actor name in the audit log ("system:billing-run").</summary>
    string Name { get; }
    string Description { get; }
    /// <summary>Do the work; returns a one-line summary of what was done.</summary>
    Task<string> RunAsync(CancellationToken ct);
}
