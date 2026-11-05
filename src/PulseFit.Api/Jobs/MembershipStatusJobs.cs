using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;
using PulseFit.Api.Services;

namespace PulseFit.Api.Jobs;

/// <summary>Suspends members still unpaid when the grace period after a failed payment runs out.</summary>
public class GracePeriodJob : IGymJob
{
    private readonly GymDbContext _db;
    private readonly GymClock _clock;
    private readonly AuditService _audit;

    public GracePeriodJob(GymDbContext db, GymClock clock, AuditService audit) { _db = db; _clock = clock; _audit = audit; }

    public string Name => "grace-period";
    public string Description => $"Suspends members still unpaid {GymRules.PaymentGraceDays} days after a failed payment.";

    public async Task<string> RunAsync(CancellationToken ct)
    {
        var cutoff = _clock.Today.AddDays(-GymRules.PaymentGraceDays);
        var overdue = await _db.Members.Where(m => m.Status == MemberStatus.PastDue && m.PastDueSince <= cutoff).ToListAsync(ct);
        foreach (var m in overdue)
        {
            m.Status = MemberStatus.Suspended;
            _audit.System(Name, "member.suspended", m.Id, $"Payment failed on {m.PastDueSince:MMM d} and wasn't made within {GymRules.PaymentGraceDays} days. Access suspended.");
        }
        await _db.SaveChangesAsync(ct);
        return $"{overdue.Count} suspended.";
    }
}

/// <summary>Ends freezes that have run their course.</summary>
public class FreezeEndJob : IGymJob
{
    private readonly GymDbContext _db;
    private readonly GymClock _clock;
    private readonly AuditService _audit;

    public FreezeEndJob(GymDbContext db, GymClock clock, AuditService audit) { _db = db; _clock = clock; _audit = audit; }

    public string Name => "freeze-end";
    public string Description => "Makes frozen memberships active again when the freeze ends.";

    public async Task<string> RunAsync(CancellationToken ct)
    {
        var today = _clock.Today;
        var ending = await _db.Members.Where(m => m.Status == MemberStatus.Frozen && m.FreezeUntil <= today).ToListAsync(ct);
        foreach (var m in ending)
        {
            m.Status = MemberStatus.Active;
            _audit.System(Name, "member.unfrozen", m.Id, $"Freeze ended on {m.FreezeUntil:MMM d}.");
        }
        await _db.SaveChangesAsync(ct);
        return $"{ending.Count} back from a freeze.";
    }
}

/// <summary>Closes memberships whose notice period is over, and gives up their future class places.</summary>
public class CancellationJob : IGymJob
{
    private readonly GymDbContext _db;
    private readonly GymClock _clock;
    private readonly AuditService _audit;

    public CancellationJob(GymDbContext db, GymClock clock, AuditService audit) { _db = db; _clock = clock; _audit = audit; }

    public string Name => "cancellations";
    public string Description => "Closes memberships whose cancellation notice is over.";

    public async Task<string> RunAsync(CancellationToken ct)
    {
        var today = _clock.Today;
        var now = _clock.UtcNow;
        var leaving = await _db.Members.Where(m => m.Status != MemberStatus.Cancelled && m.CancelEffectiveOn <= today).ToListAsync(ct);
        foreach (var m in leaving)
        {
            m.Status = MemberStatus.Cancelled;
            var future = await _db.Bookings.Where(b => b.MemberId == m.Id && b.Session!.StartsAt > now
                && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted)).ToListAsync(ct);
            foreach (var b in future) { b.Status = BookingStatus.Cancelled; b.CancelledAt = now; }
            _audit.System(Name, "member.cancelled", m.Id, $"Cancellation took effect ({m.CancelRequestedOn:MMM d} + {GymRules.CancellationNoticeDays} days). {future.Count} upcoming class(es) released.");
        }
        await _db.SaveChangesAsync(ct);
        return $"{leaving.Count} cancelled.";
    }
}
