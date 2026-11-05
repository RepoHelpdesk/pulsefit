using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;
using PulseFit.Api.Services;

namespace PulseFit.Api.Jobs;

/// <summary>
/// After a class ends, anyone still booked (not marked attended, and didn't check in) is a no-show and
/// is charged the fee. Too many no-shows in a month blocks booking for a week.
/// </summary>
public class NoShowJob : IGymJob
{
    private readonly GymDbContext _db;
    private readonly GymClock _clock;
    private readonly BillingService _billing;
    private readonly AuditService _audit;

    public NoShowJob(GymDbContext db, GymClock clock, BillingService billing, AuditService audit)
    {
        _db = db; _clock = clock; _billing = billing; _audit = audit;
    }

    public string Name => "no-shows";
    public string Description => "Charges no-show fees for classes that ended, and blocks repeat no-shows.";

    public async Task<string> RunAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var missed = (await _db.Bookings.Include(b => b.Session).Include(b => b.Member)
                .Where(b => b.Status == BookingStatus.Booked && !b.Session!.Cancelled)
                .ToListAsync(ct))
            .Where(b => b.Session!.EndsAt <= now)
            .ToList();

        foreach (var b in missed)
        {
            b.Status = BookingStatus.NoShow;
            _billing.Charge(b.Member!, InvoiceKind.NoShowFee, GymRules.NoShowFee, $"No-show: {b.Session!.Name}, {_clock.ToLocal(b.Session.StartsAt):MMM d h:mm tt}");
            _audit.System(Name, "class.no_show", b.MemberId, $"Didn't come to {b.Session.Name}, {_clock.ToLocal(b.Session.StartsAt):ddd MMM d h:mm tt}. ${GymRules.NoShowFee:0} fee.");
        }
        await _db.SaveChangesAsync(ct);

        var blocked = 0;
        var since = now.AddDays(-GymRules.NoShowLookbackDays);
        foreach (var memberId in missed.Select(b => b.MemberId).Distinct())
        {
            var count = await _db.Bookings.CountAsync(b => b.MemberId == memberId && b.Status == BookingStatus.NoShow && b.Session!.StartsAt >= since, ct);
            if (count < GymRules.NoShowsBeforeBlock) continue;
            var member = await _db.Members.FindAsync([memberId], ct);
            if (member!.BookingBlockedUntil > now) continue;
            member.BookingBlockedUntil = now.AddDays(GymRules.NoShowBlockDays);
            blocked++;
            _audit.System(Name, "booking.blocked", memberId, $"{count} no-shows in {GymRules.NoShowLookbackDays} days. Can't book classes for {GymRules.NoShowBlockDays} days.");
        }
        await _db.SaveChangesAsync(ct);
        return $"{missed.Count} no-show(s) charged, {blocked} member(s) blocked from booking.";
    }
}
