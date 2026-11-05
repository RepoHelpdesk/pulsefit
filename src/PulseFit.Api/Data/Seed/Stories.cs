using PulseFit.Api.Domain;
using PulseFit.Api.Services;

namespace PulseFit.Api.Data.Seed;

/// <summary>
/// Members with a history worth asking about. Each one is what the code would have done, written out
/// as if it happened: the bills, the visits and the audit trail all agree with the rules.
/// </summary>
internal class Stories
{
    private readonly DemoSeeder _s;
    public Stories(DemoSeeder seeder) { _s = seeder; }

    private GymDbContext Db => _s.Db;
    private GymClock Clock => _s.Clock;
    private DateOnly Today => _s.Today;
    private Location Downtown => _s.Locations[0];
    private Location Riverside => _s.Locations[1];
    private Location Northgate => _s.Locations[2];

    public async Task AddAsync()
    {
        LateCancel();
        WrongLocation();
        Suspended();
        await ChargedAfterCancelling();
        await YearlyUpgrade();
        await PromotedAfterStart();
        RepeatNoShows();
        Student();
        FreezeLimit();
        GuestPasses();
    }

    /// <summary>Dana cancelled yesterday's HIIT two hours before it started: a late cancel, $10.</summary>
    private void LateCancel()
    {
        var dana = Member("Dana", "Whitfield", PlanTier.Plus, Downtown, joinedDaysAgo: 300);
        dana.Notes = "Regular at the evening HIIT. Called the desk about the late-cancel fee.";
        var session = Past(Downtown, "HIIT Blast", daysAgo: 1);
        var cancelledAt = session.StartsAt.AddHours(-2);
        Db.Bookings.Add(new Booking { Session = session, Member = dana, Status = BookingStatus.LateCancelled, CreatedAt = session.StartsAt.AddDays(-2), CancelledAt = cancelledAt });
        Invoice(dana, InvoiceKind.LateCancelFee, GymRules.LateCancelFee, $"Late cancel: {session.Name}, {Local(session.StartsAt):MMM d h:mm tt}", cancelledAt);
        Audit(_s.DeskAt(Downtown), "class.booked", dana, session.StartsAt.AddDays(-2), $"{session.Name}, {Local(session.StartsAt):ddd MMM d h:mm tt}.");
        Audit(_s.DeskAt(Downtown), "class.late_cancelled", dana, cancelledAt,
            $"{session.Name}, {Local(session.StartsAt):ddd MMM d h:mm tt} — under {GymRules.LateCancelWindow.TotalHours:0} hours before the start, ${GymRules.LateCancelFee:0} fee.");
    }

    /// <summary>Marcus is on Basic at Riverside and tried to get in at Downtown this morning.</summary>
    private void WrongLocation()
    {
        var marcus = Member("Marcus", "Lee", PlanTier.Basic, Riverside, joinedDaysAgo: 90);
        var at = Clock.ToUtc(Today.ToDateTime(new TimeOnly(7, 40)));
        if (at > Clock.UtcNow) at = at.AddDays(-1);
        var reason = $"Basic only includes {Riverside.Name}. Upgrade to Plus for every location.";
        Db.CheckIns.Add(new CheckIn { Member = marcus, LocationId = Downtown.Id, At = at, Allowed = false, DeniedReason = reason, StaffId = _s.DeskAt(Downtown).Id });
        Db.CheckIns.Add(new CheckIn { Member = marcus, LocationId = Riverside.Id, At = at.AddDays(-2), Allowed = true, StaffId = _s.DeskAt(Riverside).Id });
        Audit(_s.DeskAt(Downtown), "checkin.denied", marcus, at, $"Turned away at {Downtown.Name}: {reason}");
        BillingHistory(marcus);
    }

    /// <summary>Priya's card expired; the payment failed a week ago and the grace period ran out.</summary>
    private void Suspended()
    {
        var priya = Member("Priya", "Shah", PlanTier.Premium, Northgate, joinedDaysAgo: 400);
        var failedOn = Today.AddDays(-7);
        priya.JoinedOn = failedOn.AddMonths(-13);
        priya.NextBillingDate = failedOn.AddMonths(1);
        priya.CardStatus = CardStatus.Expired;
        priya.CardLast4 = "4417";
        priya.Status = MemberStatus.Suspended;
        priya.PastDueSince = failedOn;
        var failedAt = Clock.ToUtc(failedOn.ToDateTime(new TimeOnly(2, 0)));
        Invoice(priya, InvoiceKind.Membership, 79m, $"Premium (monthly), {failedOn:MMM d, yyyy} – {failedOn.AddMonths(1):MMM d, yyyy}", failedAt, "Card expired");
        Invoice(priya, InvoiceKind.Membership, 79m, $"Premium (monthly), {failedOn.AddMonths(-1):MMM d, yyyy} – {failedOn:MMM d, yyyy}", failedAt.AddMonths(-1));
        System("billing-run", "billing.failed", priya, failedAt, $"Premium (monthly), {failedOn:MMM d, yyyy} – {failedOn.AddMonths(1):MMM d, yyyy}: $79.00 — Card expired. Next bill {priya.NextBillingDate:yyyy-MM-dd}.");
        var suspendedAt = Clock.ToUtc(failedOn.AddDays(GymRules.PaymentGraceDays).ToDateTime(new TimeOnly(2, 15)));
        System("grace-period", "member.suspended", priya, suspendedAt, $"Payment failed on {failedOn:MMM d} and wasn't made within {GymRules.PaymentGraceDays} days. Access suspended.");
        var deniedAt = Clock.UtcNow.AddHours(-1);
        Db.CheckIns.Add(new CheckIn { Member = priya, LocationId = Northgate.Id, At = deniedAt, Allowed = false, StaffId = _s.DeskAt(Northgate).Id,
            DeniedReason = "Membership is suspended for an unpaid balance of $79.00. Update the card to restore access." });
    }

    /// <summary>
    /// Tom gave his 30 days' notice, and his billing date fell on his last day. He was billed for a month
    /// he never got.
    /// </summary>
    private async Task ChargedAfterCancelling()
    {
        var tom = Member("Tom", "Becker", PlanTier.Plus, Downtown, joinedDaysAgo: 0);
        var leftOn = Today.AddDays(-3);
        var requestedOn = leftOn.AddDays(-GymRules.CancellationNoticeDays);
        tom.JoinedOn = leftOn.AddMonths(-14);
        tom.Status = MemberStatus.Cancelled;
        tom.CancelRequestedOn = requestedOn;
        tom.CancelEffectiveOn = leftOn;
        tom.NextBillingDate = leftOn.AddMonths(1);
        tom.Notes = "Moved to Denver. Emailed asking why he was charged after cancelling.";
        await Db.SaveChangesAsync();

        for (var i = 3; i >= 1; i--)
        {
            var from = leftOn.AddMonths(-i);
            Invoice(tom, InvoiceKind.Membership, 49m, $"Plus (monthly), {from:MMM d, yyyy} – {from.AddMonths(1):MMM d, yyyy}", At(from, 2));
        }
        Audit(_s.ManagerAt(Downtown), "member.cancel_requested", tom, At(requestedOn, 15),
            $"Cancels on {leftOn:yyyy-MM-dd} ({GymRules.CancellationNoticeDays} days' notice). Reason: Moving out of state.");
        Invoice(tom, InvoiceKind.Membership, 49m, $"Plus (monthly), {leftOn:MMM d, yyyy} – {leftOn.AddMonths(1):MMM d, yyyy}", At(leftOn, 2));
        System("billing-run", "billing.charged", tom, At(leftOn, 2), $"Plus (monthly), {leftOn:MMM d, yyyy} – {leftOn.AddMonths(1):MMM d, yyyy}: $49.00. Next bill {leftOn.AddMonths(1):yyyy-MM-dd}.");
        System("cancellations", "member.cancelled", tom, At(leftOn, 2).AddMinutes(1),
            $"Cancellation took effect ({requestedOn:MMM d} + {GymRules.CancellationNoticeDays} days). 0 upcoming class(es) released.");
    }

    /// <summary>Elena moved her yearly Plus to Premium ten days ago and was charged far more than the difference.</summary>
    private async Task YearlyUpgrade()
    {
        var elena = Member("Elena", "Rossi", PlanTier.Premium, Riverside, joinedDaysAgo: 150, BillingCycle.Yearly);
        await Db.SaveChangesAsync();
        Invoice(elena, InvoiceKind.Membership, 490m, $"Plus (yearly), {elena.JoinedOn:MMM d, yyyy} – {elena.JoinedOn.AddYears(1):MMM d, yyyy}", At(elena.JoinedOn, 10));

        var upgradedOn = Today.AddDays(-10);
        var charge = MembershipService.Proration(490m, 790m, upgradedOn, elena.NextBillingDate);
        var manager = _s.ManagerAt(Riverside);
        var invoice = Invoice(elena, InvoiceKind.Proration, charge, "Upgrade Plus → Premium, rest of this cycle", At(upgradedOn, 16));
        Audit(manager, "plan.upgraded", elena, At(upgradedOn, 16), $"Upgraded Plus → Premium now; charged ${charge:0.00} for the rest of the cycle.");
        elena.Notes = "Says the upgrade charge is wrong. Refund request sent to Rachel while we look into it.";

        await Db.SaveChangesAsync();
        Db.Refunds.Add(new Refund
        {
            InvoiceId = invoice.Id, Amount = charge, Reason = "Member disputes the upgrade charge", Status = RefundStatus.Pending,
            RequestedByStaffId = manager.Id, CreatedAt = At(Today.AddDays(-1), 11),
        });
        Audit(manager, "refund.requested", elena, At(Today.AddDays(-1), 11),
            $"Asked the owner to approve a ${charge:0.00} refund of Upgrade Plus → Premium, rest of this cycle (over the ${GymRules.ManagerRefundLimit:0} manager limit). Reason: Member disputes the upgrade charge");
    }

    /// <summary>
    /// Sam was on the waitlist for last night's full Strength Lab. Someone cancelled ten minutes in; Sam got
    /// the place, never knew, and was charged as a no-show.
    /// </summary>
    private async Task PromotedAfterStart()
    {
        var sam = Member("Sam", "Ortiz", PlanTier.Premium, Downtown, joinedDaysAgo: 220);
        await Db.SaveChangesAsync();
        var session = Past(Downtown, "Strength Lab", daysAgo: 1);
        var holders = Db.Bookings.Local.Where(b => b.Session == session && b.Status is BookingStatus.Attended or BookingStatus.NoShow).ToList();
        foreach (var extra in Db.Members.Local.Where(m => m.Plan!.Tier == PlanTier.Premium && m.Status == MemberStatus.Active && holders.All(h => h.MemberId != m.Id)).Take(session.Capacity - holders.Count))
        {
            var b = new Booking { Session = session, MemberId = extra.Id, Status = BookingStatus.Attended, CreatedAt = session.StartsAt.AddDays(-3) };
            Db.Bookings.Add(b);
            holders.Add(b);
        }

        var canceller = holders.First(b => b.Status == BookingStatus.Attended);
        var cancelledAt = session.StartsAt.AddMinutes(10);
        canceller.Status = BookingStatus.LateCancelled;
        canceller.CancelledAt = cancelledAt;
        var cancellerMember = Db.Members.Local.First(m => m.Id == canceller.MemberId);
        Invoice(cancellerMember, InvoiceKind.LateCancelFee, GymRules.LateCancelFee, $"Late cancel: {session.Name}, {Local(session.StartsAt):MMM d h:mm tt}", cancelledAt);
        Audit(_s.DeskAt(Downtown), "class.late_cancelled", cancellerMember, cancelledAt,
            $"{session.Name}, {Local(session.StartsAt):ddd MMM d h:mm tt} — under {GymRules.LateCancelWindow.TotalHours:0} hours before the start, ${GymRules.LateCancelFee:0} fee.");

        var waitlistedAt = session.StartsAt.AddDays(-2);
        Db.Bookings.Add(new Booking { Session = session, Member = sam, Status = BookingStatus.NoShow, CreatedAt = waitlistedAt, PromotedAt = cancelledAt });
        Audit(_s.DeskAt(Downtown), "class.waitlisted", sam, waitlistedAt, $"{session.Name}, {Local(session.StartsAt):ddd MMM d h:mm tt} — class full, added to the waitlist.");
        System("waitlist", "class.promoted", sam, cancelledAt, $"Got a place on {session.Name}, {Local(session.StartsAt):ddd MMM d h:mm tt}, from the waitlist.");
        var noShowAt = session.EndsAt.AddMinutes(7);
        Invoice(sam, InvoiceKind.NoShowFee, GymRules.NoShowFee, $"No-show: {session.Name}, {Local(session.StartsAt):MMM d h:mm tt}", noShowAt);
        System("no-shows", "class.no_show", sam, noShowAt, $"Didn't come to {session.Name}, {Local(session.StartsAt):ddd MMM d h:mm tt}. ${GymRules.NoShowFee:0} fee.");
        sam.Notes = "Upset about a no-show fee: says they were only on the waitlist.";
    }

    /// <summary>Jordan missed three classes in under two weeks, so booking is blocked for a week.</summary>
    private void RepeatNoShows()
    {
        var jordan = Member("Jordan", "Kim", PlanTier.Plus, Northgate, joinedDaysAgo: 120);
        DateTime last = default;
        foreach (var daysAgo in new[] { 12, 8, 3 })
        {
            var session = Past(Northgate, null, daysAgo);
            Db.Bookings.Add(new Booking { Session = session, Member = jordan, Status = BookingStatus.NoShow, CreatedAt = session.StartsAt.AddDays(-1) });
            last = session.EndsAt.AddMinutes(7);
            Invoice(jordan, InvoiceKind.NoShowFee, GymRules.NoShowFee, $"No-show: {session.Name}, {Local(session.StartsAt):MMM d h:mm tt}", last);
            System("no-shows", "class.no_show", jordan, last, $"Didn't come to {session.Name}, {Local(session.StartsAt):ddd MMM d h:mm tt}. ${GymRules.NoShowFee:0} fee.");
        }
        jordan.BookingBlockedUntil = last.AddDays(GymRules.NoShowBlockDays);
        System("no-shows", "booking.blocked", jordan, last, $"3 no-shows in {GymRules.NoShowLookbackDays} days. Can't book classes for {GymRules.NoShowBlockDays} days.");
    }

    /// <summary>Aisha has the student discount, so a promo code can't go on top.</summary>
    private void Student()
    {
        var aisha = Member("Aisha", "Patel", PlanTier.Plus, Riverside, joinedDaysAgo: 60);
        aisha.Discount = DiscountType.Student;
        aisha.Notes = "Asked whether she can use NEWYEAR30 as well as her student discount.";
        BillingHistory(aisha);
    }

    /// <summary>Ben froze in the summer and again now: both of this year's freeze months are used.</summary>
    private void FreezeLimit()
    {
        var ben = Member("Ben", "Carter", PlanTier.Premium, Riverside, joinedDaysAgo: 500);
        ben.Status = MemberStatus.Frozen;
        ben.FreezeFrom = Today.AddDays(-10);
        ben.FreezeUntil = ben.FreezeFrom.Value.AddMonths(1);
        ben.FreezeYear = Today.Year;
        ben.FreezeMonthsUsed = 2;
        var summer = new DateOnly(Today.Year, 7, 3);
        var manager = _s.ManagerAt(Riverside);
        Audit(manager, "member.frozen", ben, At(summer, 14), $"Frozen for 1 month(s), until {summer.AddMonths(1):yyyy-MM-dd}. Billed ${GymRules.FreezeFeePerMonth:0} a month meanwhile.");
        Audit(manager, "member.frozen", ben, At(ben.FreezeFrom.Value, 14), $"Frozen for 1 month(s), until {ben.FreezeUntil:yyyy-MM-dd}. Billed ${GymRules.FreezeFeePerMonth:0} a month meanwhile.");
        Invoice(ben, InvoiceKind.FreezeFee, GymRules.FreezeFeePerMonth, $"Freeze fee, {summer:MMM d} – {summer.AddMonths(1):MMM d}", At(summer, 14));
        Invoice(ben, InvoiceKind.FreezeFee, GymRules.FreezeFeePerMonth, $"Freeze fee, {ben.FreezeFrom:MMM d} – {ben.FreezeUntil:MMM d}", At(ben.FreezeFrom.Value, 14));
        ben.Notes = "Knee surgery recovery. Wants to extend the freeze into next month.";
    }

    /// <summary>Leo used both of this month's guest passes, then paid for a day pass.</summary>
    private void GuestPasses()
    {
        var leo = Member("Leo", "Martins", PlanTier.Premium, Northgate, joinedDaysAgo: 340);
        var monthStart = new DateOnly(Today.Year, Today.Month, 1);
        var first = Today.AddDays(-6) < monthStart ? monthStart : Today.AddDays(-6);
        var second = Today.AddDays(-2) < monthStart ? monthStart : Today.AddDays(-2);
        Db.GuestPasses.Add(new GuestPass { MemberId = leo.Id, GuestName = "Carlos Martins", LocationId = Northgate.Id, UsedAt = At(first, 18) });
        Db.GuestPasses.Add(new GuestPass { MemberId = leo.Id, GuestName = "Ana Souza", LocationId = Northgate.Id, UsedAt = At(second, 18) });
        var desk = _s.DeskAt(Northgate);
        Audit(desk, "guest.pass_used", leo, At(first, 18), "Guest pass for Carlos Martins (1 of 2 this month).");
        Audit(desk, "guest.pass_used", leo, At(second, 18), "Guest pass for Ana Souza (2 of 2 this month).");
        var dayPassAt = Clock.UtcNow.AddHours(-3);
        Invoice(leo, InvoiceKind.DayPass, GymRules.DayPassPrice, "Day pass for Carlos Martins", dayPassAt);
        Audit(desk, "guest.day_pass", leo, dayPassAt, $"{leo.FirstName} has used all 2 guest passes for {Local(dayPassAt):MMMM}; sold a ${GymRules.DayPassPrice:0} day pass for Carlos Martins.");
        BillingHistory(leo);
    }

    // ---- Helpers ------------------------------------------------------------

    private Member Member(string first, string last, PlanTier tier, Location home, int joinedDaysAgo, BillingCycle cycle = BillingCycle.Monthly)
    {
        var joined = Today.AddDays(-joinedDaysAgo);
        var m = new Member
        {
            FirstName = first, LastName = last, Email = DemoSeeder.Email($"{first} {last}", "mail.example"), Phone = "(555) 010-" + (1000 + first.Length * 37 + last.Length),
            HomeLocation = home, HomeLocationId = home.Id, Plan = _s.Plans[tier], BillingCycle = cycle, JoinedOn = joined,
            NextBillingDate = DemoSeeder.NextBillingAfter(joined, cycle, Today), Status = MemberStatus.Active,
            CardStatus = CardStatus.Valid, CardLast4 = (1000 + (first.GetHashCode() & 0x1FFF) % 9000).ToString(),
        };
        if (cycle == BillingCycle.Yearly) m.ContractEndsOn = m.NextBillingDate;
        Db.Members.Add(m);
        Db.SaveChanges(); // the audit trail needs the member's id
        return m;
    }

    /// <summary>The last three months' paid bills.</summary>
    private void BillingHistory(Member m)
    {
        for (var i = 1; i <= 3; i++)
        {
            var from = m.NextBillingDate.AddMonths(-i);
            if (from < m.JoinedOn) break;
            Invoice(m, InvoiceKind.Membership, _s.Price(m), $"{m.Plan!.Name} (monthly), {from:MMM d, yyyy} – {from.AddMonths(1):MMM d, yyyy}", At(from, 2));
        }
    }

    /// <summary>A class that already happened at the location, on or before that many days ago.</summary>
    private ClassSession Past(Location l, string? name, int daysAgo)
    {
        var day = Today.AddDays(-daysAgo);
        return _s.Sessions
            .Where(s => s.Location == l && (name is null || s.Name == name) && DateOnly.FromDateTime(Local(s.StartsAt)) <= day)
            .OrderByDescending(s => s.StartsAt)
            .First();
    }

    private Invoice Invoice(Member m, InvoiceKind kind, decimal amount, string description, DateTime at, string? failure = null)
    {
        var invoice = new Invoice
        {
            Member = m, Kind = kind, Amount = amount, Description = description, IssuedAt = at,
            Status = failure is null ? InvoiceStatus.Paid : InvoiceStatus.Failed,
            PaidAt = failure is null ? at : null, FailedAt = failure is null ? null : at, FailureReason = failure,
        };
        Db.Invoices.Add(invoice);
        return invoice;
    }

    private void Audit(Staff by, string action, Member m, DateTime at, string details) =>
        Db.Audit.Add(new AuditEntry { At = at, Actor = $"{by.Name} ({by.Role})", StaffId = by.Id, Action = action, Details = details, MemberId = m.Id });

    private void System(string job, string action, Member m, DateTime at, string details) =>
        Db.Audit.Add(new AuditEntry { At = at, Actor = $"system:{job}", Action = action, Details = details, MemberId = m.Id });

    private DateTime At(DateOnly day, int hour) => Clock.ToUtc(day.ToDateTime(new TimeOnly(hour, 0)));
    private DateTime Local(DateTime utc) => Clock.ToLocal(utc);
}
