using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;
using PulseFit.Api.Dtos;
using PulseFit.Api.Services;

namespace PulseFit.Api.Queries;

public record CountBy(string Label, int Count);
public record MoneyBy(string Label, decimal Amount);

public record GymReport(
    int ActiveMembers, int NewLast30Days, int CancelledLast30Days, int Leaving, decimal ChurnPercent,
    IReadOnlyList<CountBy> ByStatus, IReadOnlyList<CountBy> ByPlan, IReadOnlyList<MoneyBy> RevenueByLocation,
    decimal RevenueLast30Days, decimal FailedOutstanding, decimal ClassFillPercent, IReadOnlyList<CountBy> CheckInsByDay);

public class ReportQueries
{
    private readonly GymDbContext _db;
    private readonly GymClock _clock;
    public ReportQueries(GymDbContext db, GymClock clock) { _db = db; _clock = clock; }

    public async Task<GymReport> SummaryAsync()
    {
        var today = _clock.Today;
        var now = _clock.UtcNow;
        var since = today.AddDays(-30);
        var sinceUtc = now.AddDays(-30);

        var members = await _db.Members.Include(m => m.Plan).Include(m => m.HomeLocation).ToListAsync();
        var current = members.Where(m => m.Status != MemberStatus.Cancelled).ToList();
        var cancelled30 = members.Count(m => m.Status == MemberStatus.Cancelled && m.CancelEffectiveOn >= since);
        var startOfPeriod = current.Count + cancelled30;

        // Revenue is what was paid, less refunds, by the member's home gym.
        var paid = await _db.Invoices.Include(i => i.Member).ThenInclude(m => m!.HomeLocation)
            .Where(i => i.IssuedAt >= sinceUtc && (i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.PartiallyRefunded || i.Status == InvoiceStatus.Refunded))
            .ToListAsync();
        var failed = await _db.Invoices.Where(i => i.Status == InvoiceStatus.Failed).Select(i => i.Amount).ToListAsync();

        var weekAgo = now.AddDays(-7);
        var sessions = await _db.Sessions.Where(s => !s.Cancelled && s.StartsAt >= weekAgo && s.StartsAt < now)
            .Select(s => new { s.Capacity, Came = _db.Bookings.Count(b => b.SessionId == s.Id && b.Status == BookingStatus.Attended) })
            .ToListAsync();

        var checkIns = await _db.CheckIns.Where(c => c.Allowed && c.At >= weekAgo).Select(c => c.At).ToListAsync();

        return new GymReport(
            ActiveMembers: current.Count(m => m.Status is MemberStatus.Active or MemberStatus.PastDue),
            NewLast30Days: members.Count(m => m.JoinedOn >= since),
            CancelledLast30Days: cancelled30,
            Leaving: current.Count(m => m.CancelEffectiveOn is not null),
            ChurnPercent: startOfPeriod == 0 ? 0 : Math.Round(100m * cancelled30 / startOfPeriod, 1),
            ByStatus: current.GroupBy(m => m.Status.ToString()).Select(g => new CountBy(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList(),
            ByPlan: current.GroupBy(m => m.Plan!.Name).Select(g => new CountBy(g.Key, g.Count())).OrderBy(x => x.Label).ToList(),
            RevenueByLocation: paid.GroupBy(i => i.Member!.HomeLocation!.Name).Select(g => new MoneyBy(g.Key, g.Sum(i => i.Amount - i.RefundedAmount))).OrderBy(x => x.Label).ToList(),
            RevenueLast30Days: paid.Sum(i => i.Amount - i.RefundedAmount),
            FailedOutstanding: failed.Sum(),
            ClassFillPercent: sessions.Sum(s => s.Capacity) == 0 ? 0 : Math.Round(100m * sessions.Sum(s => s.Came) / sessions.Sum(s => s.Capacity), 1),
            CheckInsByDay: checkIns.GroupBy(at => DateOnly.FromDateTime(_clock.ToLocal(at))).OrderBy(g => g.Key)
                .Select(g => new CountBy(g.Key.ToString("ddd MMM d"), g.Count())).ToList());
    }

    public async Task<Paged<AuditDto>> AuditAsync(int? memberId, string? actor, string? action, int skip, int take)
    {
        var q = _db.Audit.AsQueryable();
        if (memberId is not null) q = q.Where(a => a.MemberId == memberId);
        if (!string.IsNullOrWhiteSpace(actor)) q = q.Where(a => a.Actor.ToLower().Contains(actor.ToLower()));
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action.StartsWith(action));
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(a => a.At).Skip(skip).Take(Math.Clamp(take, 1, 100))
            .Select(a => new AuditDto(a.Id, a.At, a.Actor, a.Action, a.MemberId,
                _db.Members.Where(m => m.Id == a.MemberId).Select(m => m.FirstName + " " + m.LastName).FirstOrDefault(), a.Details))
            .ToListAsync();
        return new Paged<AuditDto>(items, total);
    }

    public Task<List<CheckInDto>> RecentCheckInsAsync(int? locationId, int take) =>
        _db.CheckIns.Where(c => locationId == null || c.LocationId == locationId).OrderByDescending(c => c.At).Take(take)
            .Select(c => new CheckInDto(c.Id, c.MemberId, c.Member!.FirstName + " " + c.Member.LastName, c.Location!.Name, c.At, c.Allowed, c.DeniedReason))
            .ToListAsync();
}
