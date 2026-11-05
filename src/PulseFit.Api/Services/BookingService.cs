using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;

namespace PulseFit.Api.Services;

/// <summary>Class bookings, the waitlist, attendance and late cancels.</summary>
public class BookingService
{
    private readonly GymDbContext _db;
    private readonly GymClock _clock;
    private readonly BillingService _billing;
    private readonly AuditService _audit;

    public BookingService(GymDbContext db, GymClock clock, BillingService billing, AuditService audit)
    {
        _db = db; _clock = clock; _billing = billing; _audit = audit;
    }

    private static readonly BookingStatus[] Holding = [BookingStatus.Booked, BookingStatus.Attended];

    /// <summary>
    /// Book a member into a class, or onto its waitlist when it's full. Booking opens
    /// <see cref="GymRules.BookingOpensDaysAhead"/> days ahead; the member's plan decides how many classes a
    /// month they get.
    /// </summary>
    public async Task<Booking> BookAsync(Staff by, int sessionId, int memberId)
    {
        by.Require(Permission.BookClasses);
        var session = await _db.Sessions.FindAsync(sessionId) ?? throw new NotFoundException("Class");
        var member = await _db.Members.Include(m => m.Plan).FirstOrDefaultAsync(m => m.Id == memberId) ?? throw new NotFoundException("Member");
        var now = _clock.UtcNow;

        if (session.Cancelled) throw new GymRuleException("class_cancelled", "This class was cancelled.");
        if (session.StartsAt <= now) throw new GymRuleException("class_started", "This class has already started.");
        if (session.StartsAt > now.AddDays(GymRules.BookingOpensDaysAhead))
            throw new GymRuleException("too_early", $"Booking opens {GymRules.BookingOpensDaysAhead} days before the class, on {_clock.ToLocal(session.StartsAt.AddDays(-GymRules.BookingOpensDaysAhead)):ddd MMM d, h:mm tt}.");
        if (member.Status is MemberStatus.Frozen or MemberStatus.Suspended or MemberStatus.Cancelled)
            throw new GymRuleException("not_active", $"{member.FirstName}'s membership is {member.Status.ToString().ToLower()}, so they can't book classes.");
        if (member.BookingBlockedUntil > now)
            throw new GymRuleException("booking_blocked",
                $"{member.FirstName} missed {GymRules.NoShowsBeforeBlock} classes in {GymRules.NoShowLookbackDays} days, so booking is blocked until {_clock.ToLocal(member.BookingBlockedUntil.Value):ddd MMM d}.");

        var plan = member.Plan!;
        if (plan.ClassesPerMonth == 0)
            throw new GymRuleException("plan_no_classes", $"{plan.Name} doesn't include classes. Upgrade to Plus or Premium.");
        if (plan.ClassesPerMonth is { } limit)
        {
            var local = _clock.ToLocal(session.StartsAt);
            var monthStart = _clock.ToUtc(new DateTime(local.Year, local.Month, 1));
            var monthEnd = _clock.ToUtc(new DateTime(local.Year, local.Month, 1).AddMonths(1));
            var used = await _db.Bookings.CountAsync(b => b.MemberId == memberId
                && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Attended || b.Status == BookingStatus.NoShow)
                && b.Session!.StartsAt >= monthStart && b.Session.StartsAt < monthEnd);
            if (used >= limit)
                throw new GymRuleException("class_limit", $"{plan.Name} includes {limit} classes a month and {member.FirstName} has booked {used} in {local:MMMM}.");
        }

        if (await _db.Bookings.AnyAsync(b => b.SessionId == sessionId && b.MemberId == memberId
                && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted)))
            throw new GymRuleException("already_booked", $"{member.FirstName} is already on this class.");

        var taken = await _db.Bookings.CountAsync(b => b.SessionId == sessionId && Holding.Contains(b.Status));
        var booking = new Booking
        {
            SessionId = sessionId, MemberId = memberId, CreatedAt = now,
            Status = taken < session.Capacity ? BookingStatus.Booked : BookingStatus.Waitlisted,
        };
        _db.Bookings.Add(booking);
        _audit.Staff(by, booking.Status == BookingStatus.Booked ? "class.booked" : "class.waitlisted", memberId,
            $"{session.Name}, {_clock.ToLocal(session.StartsAt):ddd MMM d h:mm tt}{(booking.Status == BookingStatus.Waitlisted ? " — class full, added to the waitlist" : "")}.");
        await _db.SaveChangesAsync();
        return booking;
    }

    /// <summary>
    /// Cancel a booking. Inside <see cref="GymRules.LateCancelWindow"/> of the start it's a late cancel
    /// and the fee is charged. The place goes to the first person on the waitlist.
    /// </summary>
    public async Task CancelAsync(Staff by, int bookingId)
    {
        by.Require(Permission.BookClasses);
        var booking = await _db.Bookings.Include(b => b.Session).Include(b => b.Member)
            .FirstOrDefaultAsync(b => b.Id == bookingId) ?? throw new NotFoundException("Booking");
        if (booking.Status is not (BookingStatus.Booked or BookingStatus.Waitlisted))
            throw new GymRuleException("not_cancellable", $"This booking is already {booking.Status.ToString().ToLower()}.");

        var session = booking.Session!;
        var now = _clock.UtcNow;
        if (session.EndsAt <= now) throw new GymRuleException("class_over", "This class is over. Mark the booking as attended or no-show instead.");

        var hadPlace = booking.Status == BookingStatus.Booked;
        var late = hadPlace && session.StartsAt - now < GymRules.LateCancelWindow;
        booking.Status = late ? BookingStatus.LateCancelled : BookingStatus.Cancelled;
        booking.CancelledAt = now;

        if (late)
            _billing.Charge(booking.Member!, InvoiceKind.LateCancelFee, GymRules.LateCancelFee,
                $"Late cancel: {session.Name}, {_clock.ToLocal(session.StartsAt):MMM d h:mm tt}");
        _audit.Staff(by, late ? "class.late_cancelled" : "class.cancelled", booking.MemberId,
            $"{session.Name}, {_clock.ToLocal(session.StartsAt):ddd MMM d h:mm tt}{(late ? $" — under {GymRules.LateCancelWindow.TotalHours:0} hours before the start, ${GymRules.LateCancelFee:0} fee" : "")}.");

        if (hadPlace) await PromoteFromWaitlistAsync(session);
        await _db.SaveChangesAsync();
    }

    /// <summary>A place opened up: give it to whoever has waited longest.</summary>
    private async Task PromoteFromWaitlistAsync(ClassSession session)
    {
        var next = await _db.Bookings
            .Where(b => b.SessionId == session.Id && b.Status == BookingStatus.Waitlisted)
            .OrderBy(b => b.CreatedAt)
            .FirstOrDefaultAsync();
        if (next is null) return;

        next.Status = BookingStatus.Booked;
        next.PromotedAt = _clock.UtcNow;
        _audit.System("waitlist", "class.promoted", next.MemberId,
            $"Got a place on {session.Name}, {_clock.ToLocal(session.StartsAt):ddd MMM d h:mm tt}, from the waitlist.");
    }

    /// <summary>The trainer's roster: who came. Anyone left booked when the class ends is a no-show (see NoShowJob).</summary>
    public async Task MarkAsync(Staff by, int bookingId, bool attended)
    {
        by.Require(Permission.MarkAttendance);
        var booking = await _db.Bookings.Include(b => b.Session).FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new NotFoundException("Booking");
        if (by.Role == StaffRole.Trainer && booking.Session!.TrainerId != by.Id)
            throw new GymRuleException("not_your_class", "Trainers can only take attendance for their own classes.");
        if (booking.Status is not (BookingStatus.Booked or BookingStatus.Attended or BookingStatus.NoShow))
            throw new GymRuleException("not_markable", "Only a booked place can be marked.");
        if (booking.Session!.StartsAt > _clock.UtcNow)
            throw new GymRuleException("not_started", "Attendance can be taken once the class starts.");

        booking.Status = attended ? BookingStatus.Attended : BookingStatus.NoShow;
        _audit.Staff(by, attended ? "class.attended" : "class.no_show_marked", booking.MemberId, $"{booking.Session.Name}, {_clock.ToLocal(booking.Session.StartsAt):MMM d h:mm tt}.");
        await _db.SaveChangesAsync();
    }

    /// <summary>Cancel a whole class. Nobody is charged, and nobody is moved anywhere else.</summary>
    public async Task CancelSessionAsync(Staff by, int sessionId, string reason)
    {
        by.Require(Permission.ManageClasses);
        var session = await _db.Sessions.FindAsync(sessionId) ?? throw new NotFoundException("Class");
        if (session.Cancelled) return;
        session.Cancelled = true;
        var bookings = await _db.Bookings.Where(b => b.SessionId == sessionId
            && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted)).ToListAsync();
        foreach (var b in bookings)
        {
            b.Status = BookingStatus.Cancelled;
            b.CancelledAt = _clock.UtcNow;
            _audit.Staff(by, "class.cancelled_by_gym", b.MemberId, $"{session.Name}, {_clock.ToLocal(session.StartsAt):MMM d h:mm tt} was cancelled by the gym: {reason}. No fee.");
        }
        await _db.SaveChangesAsync();
    }

    /// <summary>Checking in at the gym counts as attending a class there that starts within the hour or began in the last 15 minutes.</summary>
    public async Task<int> MarkAttendedOnCheckInAsync(int memberId, int locationId)
    {
        var now = _clock.UtcNow;
        var bookings = await _db.Bookings.Include(b => b.Session)
            .Where(b => b.MemberId == memberId && b.Status == BookingStatus.Booked && b.Session!.LocationId == locationId
                && b.Session.StartsAt <= now.AddHours(1) && b.Session.StartsAt >= now.AddMinutes(-15))
            .ToListAsync();
        foreach (var b in bookings) b.Status = BookingStatus.Attended;
        return bookings.Count;
    }
}
