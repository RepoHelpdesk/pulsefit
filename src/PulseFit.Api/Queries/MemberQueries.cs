using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;
using PulseFit.Api.Dtos;
using PulseFit.Api.Services;

namespace PulseFit.Api.Queries;

/// <summary>Read-only views of members for the screens.</summary>
public class MemberQueries
{
    private readonly GymDbContext _db;
    private readonly GymClock _clock;

    public MemberQueries(GymDbContext db, GymClock clock) { _db = db; _clock = clock; }

    public async Task<Paged<MemberRow>> SearchAsync(string? q, MemberStatus? status, int? planId, int? locationId, int skip, int take)
    {
        var query = _db.Members.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(m => (m.FirstName + " " + m.LastName).ToLower().Contains(term) || m.Email.ToLower().Contains(term) || m.Phone.Contains(term));
        }
        if (status is not null) query = query.Where(m => m.Status == status);
        if (planId is not null) query = query.Where(m => m.PlanId == planId);
        if (locationId is not null) query = query.Where(m => m.HomeLocationId == locationId);

        var now = _clock.UtcNow;
        var total = await query.CountAsync();
        var items = await query.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).Skip(skip).Take(Math.Clamp(take, 1, 100))
            .Select(m => new MemberRow(m.Id, m.FirstName + " " + m.LastName, m.Email, m.Plan!.Name, m.BillingCycle, m.Status,
                m.HomeLocation!.Name, m.NextBillingDate, m.CancelEffectiveOn, m.BookingBlockedUntil > now))
            .ToListAsync();
        return new Paged<MemberRow>(items, total);
    }

    public async Task<MemberDetail> GetAsync(int id)
    {
        var m = await _db.Members.Include(x => x.Plan).Include(x => x.HomeLocation).Include(x => x.PromoCode)
            .FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Member");
        var pending = m.PendingPlanId is { } pid ? await _db.Plans.FindAsync(pid) : null;

        var invoices = await _db.Invoices.Where(i => i.MemberId == id).OrderByDescending(i => i.IssuedAt).Take(50).ToListAsync();
        var bookings = await _db.Bookings.Where(b => b.MemberId == id).OrderByDescending(b => b.Session!.StartsAt).Take(30)
            .Select(b => new MemberBookingDto(b.Id, b.SessionId, b.Session!.Name, b.Session.StartsAt, b.Session.Location!.Name, b.Status, b.PromotedAt))
            .ToListAsync();
        var visits = await _db.CheckIns.Where(c => c.MemberId == id).OrderByDescending(c => c.At).Take(20)
            .Select(c => new CheckInDto(c.Id, c.MemberId, "", c.Location!.Name, c.At, c.Allowed, c.DeniedReason))
            .ToListAsync();
        var audit = await _db.Audit.Where(a => a.MemberId == id).OrderByDescending(a => a.At).Take(50)
            .Select(a => new AuditDto(a.Id, a.At, a.Actor, a.Action, a.MemberId, null, a.Details))
            .ToListAsync();

        var local = _clock.LocalNow;
        var monthStart = _clock.ToUtc(new DateTime(local.Year, local.Month, 1));
        var monthEnd = _clock.ToUtc(new DateTime(local.Year, local.Month, 1).AddMonths(1));
        var classesThisMonth = await _db.Bookings.CountAsync(b => b.MemberId == id
            && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Attended || b.Status == BookingStatus.NoShow)
            && b.Session!.StartsAt >= monthStart && b.Session.StartsAt < monthEnd);
        var guestsThisMonth = await _db.GuestPasses.CountAsync(g => g.MemberId == id && g.UsedAt >= monthStart);
        var freezeLeft = GymRules.MaxFreezeMonthsPerYear - (m.FreezeYear == _clock.Today.Year ? m.FreezeMonthsUsed : 0);

        return new MemberDetail(
            m.Id, m.FirstName, m.LastName, m.Email, m.Phone, m.HomeLocationId, m.HomeLocation!.Name,
            m.Plan!.ToDto(), pending?.ToDto(), m.BillingCycle, m.Status, m.JoinedOn,
            m.NextBillingDate, m.ContractEndsOn, m.Discount, m.PromoMonthsLeft > 0 ? m.PromoCode?.Code : null, m.PromoMonthsLeft,
            m.CardStatus, m.CardLast4, m.Status == MemberStatus.Frozen ? m.FreezeUntil : null, freezeLeft, m.PastDueSince,
            m.CancelRequestedOn, m.CancelEffectiveOn, m.BookingBlockedUntil > _clock.UtcNow ? m.BookingBlockedUntil : null,
            invoices.Where(i => i.Status == InvoiceStatus.Failed).Sum(i => i.Amount), m.Notes,
            classesThisMonth, Math.Max(0, m.Plan!.GuestPassesPerMonth - guestsThisMonth),
            invoices.Select(i => i.ToDto()).ToList(), bookings, visits, audit);
    }
}
