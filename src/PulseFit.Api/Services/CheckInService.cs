using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;

namespace PulseFit.Api.Services;

public record CheckInResult(bool Allowed, string Message, CheckIn CheckIn, int ClassesMarked);

/// <summary>The front door: who gets in, and their guests.</summary>
public class CheckInService
{
    private readonly GymDbContext _db;
    private readonly GymClock _clock;
    private readonly BillingService _billing;
    private readonly BookingService _bookings;
    private readonly AuditService _audit;

    public CheckInService(GymDbContext db, GymClock clock, BillingService billing, BookingService bookings, AuditService audit)
    {
        _db = db; _clock = clock; _billing = billing; _bookings = bookings; _audit = audit;
    }

    /// <summary>
    /// Let a member in, or turn them away with the reason. Every attempt is recorded, refused ones too,
    /// so the desk can see why someone was turned away last time.
    /// </summary>
    public async Task<CheckInResult> CheckInAsync(Staff by, int memberId, int locationId)
    {
        by.Require(Permission.CheckIn);
        var member = await _db.Members.Include(m => m.Plan).Include(m => m.HomeLocation).FirstOrDefaultAsync(m => m.Id == memberId)
            ?? throw new NotFoundException("Member");
        var location = await _db.Locations.FindAsync(locationId) ?? throw new NotFoundException("Location");

        var denied = await DenyReasonAsync(member, location);
        var checkIn = new CheckIn
        {
            MemberId = memberId, LocationId = locationId, At = _clock.UtcNow, StaffId = by.Id,
            Allowed = denied is null, DeniedReason = denied,
        };
        _db.CheckIns.Add(checkIn);

        var marked = 0;
        string message;
        if (denied is null)
        {
            marked = await _bookings.MarkAttendedOnCheckInAsync(memberId, locationId);
            message = member.Status == MemberStatus.PastDue
                ? $"Welcome, {member.FirstName}. Payment failed on {member.PastDueSince:MMM d}; access is suspended after {member.PastDueSince!.Value.AddDays(GymRules.PaymentGraceDays):MMM d} unless the card is updated."
                : $"Welcome, {member.FirstName}.";
        }
        else
        {
            message = denied;
            _audit.Staff(by, "checkin.denied", memberId, $"Turned away at {location.Name}: {denied}");
        }
        await _db.SaveChangesAsync();
        return new CheckInResult(denied is null, message, checkIn, marked);
    }

    private async Task<string?> DenyReasonAsync(Member m, Location location)
    {
        switch (m.Status)
        {
            case MemberStatus.Cancelled:
                return $"Membership was cancelled on {m.CancelEffectiveOn:MMM d, yyyy}.";
            case MemberStatus.Frozen:
                return $"Membership is frozen until {m.FreezeUntil:MMM d}. A manager can unfreeze it early.";
            case MemberStatus.Suspended:
                var owed = (await _billing.OutstandingAsync(m.Id)).Sum(i => i.Amount);
                return $"Membership is suspended for an unpaid balance of ${owed:0.00}. Update the card to restore access.";
        }

        var localTime = TimeOnly.FromDateTime(_clock.LocalNow);
        if (localTime < location.OpensAt || localTime >= location.ClosesAt)
            return $"{location.Name} is closed. Hours are {location.OpensAt:h:mm tt} – {location.ClosesAt:h:mm tt}.";

        if (!m.Plan!.AllLocations && m.HomeLocationId != location.Id)
            return $"{m.Plan.Name} only includes {m.HomeLocation!.Name}. Upgrade to Plus for every location.";

        return null;
    }

    /// <summary>
    /// Bring a guest in. Premium includes guest passes each calendar month (they don't roll over);
    /// anyone else, or a Premium member who has used theirs, can buy a day pass for the guest.
    /// </summary>
    public async Task<string> GuestAsync(Staff by, int memberId, int locationId, string guestName, bool buyDayPass)
    {
        by.Require(Permission.IssueGuestPass);
        var member = await _db.Members.Include(m => m.Plan).FirstOrDefaultAsync(m => m.Id == memberId) ?? throw new NotFoundException("Member");
        if (member.Status is not (MemberStatus.Active or MemberStatus.PastDue))
            throw new GymRuleException("not_active", $"{member.FirstName}'s membership is {member.Status.ToString().ToLower()}, so they can't bring guests.");

        var local = _clock.LocalNow;
        var monthStart = _clock.ToUtc(new DateTime(local.Year, local.Month, 1));
        var used = await _db.GuestPasses.CountAsync(g => g.MemberId == memberId && g.UsedAt >= monthStart);
        var allowance = member.Plan!.GuestPassesPerMonth;

        if (used < allowance)
        {
            _db.GuestPasses.Add(new GuestPass { MemberId = memberId, GuestName = guestName, LocationId = locationId, UsedAt = _clock.UtcNow });
            _audit.Staff(by, "guest.pass_used", memberId, $"Guest pass for {guestName} ({used + 1} of {allowance} this month).");
            await _db.SaveChangesAsync();
            return $"{guestName} is in on a guest pass. {allowance - used - 1} left this month.";
        }

        var why = allowance == 0 ? $"{member.Plan.Name} doesn't include guest passes" : $"{member.FirstName} has used all {allowance} guest passes for {local:MMMM}";
        if (!buyDayPass)
            throw new GymRuleException("no_guest_passes", $"{why}. A day pass is ${GymRules.DayPassPrice:0}.");

        var invoice = _billing.Charge(member, InvoiceKind.DayPass, GymRules.DayPassPrice, $"Day pass for {guestName}");
        if (invoice.Status == InvoiceStatus.Failed)
            throw new GymRuleException("payment_failed", $"The day pass couldn't be charged: {invoice.FailureReason}.");
        _audit.Staff(by, "guest.day_pass", memberId, $"{why}; sold a ${GymRules.DayPassPrice:0} day pass for {guestName}.");
        await _db.SaveChangesAsync();
        return $"{guestName} is in on a day pass, charged to {member.FirstName}.";
    }
}
