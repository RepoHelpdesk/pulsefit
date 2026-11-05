using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;

namespace PulseFit.Api.Services;

public record NewMember(string FirstName, string LastName, string Email, string Phone, int HomeLocationId,
    int PlanId, BillingCycle BillingCycle, DiscountType Discount, string CardLast4);

/// <summary>Joining, freezing, changing plan and cancelling.</summary>
public class MembershipService
{
    private readonly GymDbContext _db;
    private readonly GymClock _clock;
    private readonly BillingService _billing;
    private readonly AuditService _audit;

    public MembershipService(GymDbContext db, GymClock clock, BillingService billing, AuditService audit)
    {
        _db = db; _clock = clock; _billing = billing; _audit = audit;
    }

    public async Task<Member> GetAsync(int id) =>
        await _db.Members.Include(m => m.Plan).Include(m => m.HomeLocation).Include(m => m.PromoCode)
            .FirstOrDefaultAsync(m => m.Id == id) ?? throw new NotFoundException("Member");

    /// <summary>Sign a new member up. The first cycle is charged now; the next bill is a cycle from today.</summary>
    public async Task<Member> JoinAsync(Staff by, NewMember n)
    {
        by.Require(Permission.EditMembers);
        if (await _db.Members.AnyAsync(m => m.Email == n.Email))
            throw new GymRuleException("email_taken", "A member with that email already exists.");
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == n.PlanId && p.Active) ?? throw new NotFoundException("Plan");

        var today = _clock.Today;
        var member = new Member
        {
            FirstName = n.FirstName, LastName = n.LastName, Email = n.Email, Phone = n.Phone,
            HomeLocationId = n.HomeLocationId, PlanId = plan.Id, Plan = plan, BillingCycle = n.BillingCycle,
            Discount = n.Discount, CardLast4 = n.CardLast4, CardStatus = CardStatus.Valid,
            Status = MemberStatus.Active, JoinedOn = today, NextBillingDate = NextCycle(today, n.BillingCycle),
            ContractEndsOn = n.BillingCycle == BillingCycle.Yearly ? today.AddYears(1) : null,
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();

        _billing.Charge(member, InvoiceKind.Membership, _billing.MembershipPrice(member, plan),
            $"{plan.Name} ({member.BillingCycle.ToString().ToLower()}), first {(n.BillingCycle == BillingCycle.Yearly ? "year" : "month")}");
        _audit.Staff(by, "member.joined", member.Id, $"Joined on {plan.Name}, billed {member.BillingCycle.ToString().ToLower()}.");
        await _db.SaveChangesAsync();
        return member;
    }

    /// <summary>
    /// Freeze for whole months, starting today. A member gets <see cref="GymRules.MaxFreezeMonthsPerYear"/>
    /// months a calendar year; months asked for count even if the member comes back early.
    /// </summary>
    public async Task FreezeAsync(Staff by, int memberId, int months)
    {
        by.Require(Permission.FreezeMembership);
        var m = await GetAsync(memberId);
        if (m.Status != MemberStatus.Active)
            throw new GymRuleException("not_active", $"Only an active membership can be frozen; this one is {m.Status.ToString().ToLower()}.");
        if (m.CancelEffectiveOn is not null)
            throw new GymRuleException("cancelling", "This membership is being cancelled, so it can't be frozen.");

        var today = _clock.Today;
        if (m.FreezeYear != today.Year) { m.FreezeYear = today.Year; m.FreezeMonthsUsed = 0; }
        var left = GymRules.MaxFreezeMonthsPerYear - m.FreezeMonthsUsed;
        if (months < 1 || months > left)
            throw new GymRuleException("freeze_limit",
                left == 0 ? $"{m.FirstName} has used all {GymRules.MaxFreezeMonthsPerYear} freeze months for {today.Year}."
                          : $"{m.FirstName} can freeze for at most {left} more month(s) in {today.Year}.");

        m.Status = MemberStatus.Frozen;
        m.FreezeFrom = today;
        m.FreezeUntil = today.AddMonths(months);
        m.FreezeMonthsUsed += months;
        _audit.Staff(by, "member.frozen", m.Id, $"Frozen for {months} month(s), until {m.FreezeUntil:yyyy-MM-dd}. Billed ${GymRules.FreezeFeePerMonth:0} a month meanwhile.");
        await _db.SaveChangesAsync();
    }

    public async Task UnfreezeAsync(Staff by, int memberId)
    {
        by.Require(Permission.FreezeMembership);
        var m = await GetAsync(memberId);
        if (m.Status != MemberStatus.Frozen) throw new GymRuleException("not_frozen", "This membership isn't frozen.");
        m.Status = MemberStatus.Active;
        m.FreezeUntil = _clock.Today;
        _audit.Staff(by, "member.unfrozen", m.Id, "Came back early from a freeze. The months asked for still count toward this year's limit.");
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Change plan. An upgrade starts now and the difference is charged for the rest of the current cycle;
    /// a downgrade waits until the next billing date.
    /// </summary>
    public async Task ChangePlanAsync(Staff by, int memberId, int planId)
    {
        by.Require(Permission.ChangePlan);
        var m = await GetAsync(memberId);
        if (m.Status is MemberStatus.Cancelled or MemberStatus.Suspended)
            throw new GymRuleException("not_active", $"A {m.Status.ToString().ToLower()} membership can't change plan.");
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == planId && p.Active) ?? throw new NotFoundException("Plan");
        if (plan.Id == m.PlanId) throw new GymRuleException("same_plan", $"{m.FirstName} is already on {plan.Name}.");

        var oldPlan = m.Plan!;
        var oldPrice = oldPlan.PriceFor(m.BillingCycle);
        var newPrice = plan.PriceFor(m.BillingCycle);

        if (newPrice > oldPrice)
        {
            var charge = Proration(oldPrice, newPrice, _clock.Today, m.NextBillingDate);
            m.PlanId = plan.Id;
            m.Plan = plan;
            m.PendingPlanId = null;
            if (charge > 0)
                _billing.Charge(m, InvoiceKind.Proration, charge, $"Upgrade {oldPlan.Name} → {plan.Name}, rest of this cycle");
            _audit.Staff(by, "plan.upgraded", m.Id, $"Upgraded {oldPlan.Name} → {plan.Name} now; charged ${charge:0.00} for the rest of the cycle.");
        }
        else
        {
            m.PendingPlanId = plan.Id;
            _audit.Staff(by, "plan.downgrade_scheduled", m.Id, $"Moves {oldPlan.Name} → {plan.Name} on {m.NextBillingDate:yyyy-MM-dd}, the next billing date.");
        }
        await _db.SaveChangesAsync();
    }

    /// <summary>What an upgrade costs for the days left in the cycle, rounded up to the cent.</summary>
    public static decimal Proration(decimal oldPrice, decimal newPrice, DateOnly today, DateOnly nextBillingDate)
    {
        const int daysInCycle = 30;
        var daysLeft = nextBillingDate.DayNumber - today.DayNumber;
        if (daysLeft <= 0) return 0m;
        var raw = (newPrice - oldPrice) * daysLeft / daysInCycle;
        return Math.Ceiling(raw * 100m) / 100m;
    }

    /// <summary>
    /// Cancel. It takes effect after <see cref="GymRules.CancellationNoticeDays"/> days' notice, and the
    /// member is billed as normal until then. A yearly plan cancelled before its contract ends pays the
    /// early-exit fee now.
    /// </summary>
    public async Task CancelAsync(Staff by, int memberId, string reason)
    {
        by.Require(Permission.CancelMembership);
        var m = await GetAsync(memberId);
        if (m.Status == MemberStatus.Cancelled || m.CancelEffectiveOn is not null)
            throw new GymRuleException("already_cancelling", "This membership is already cancelled or being cancelled.");

        var today = _clock.Today;
        m.CancelRequestedOn = today;
        m.CancelEffectiveOn = today.AddDays(GymRules.CancellationNoticeDays);
        m.PendingPlanId = null;

        var exitFee = m.BillingCycle == BillingCycle.Yearly && m.ContractEndsOn > today;
        if (exitFee)
            _billing.Charge(m, InvoiceKind.EarlyExitFee, GymRules.YearlyEarlyExitFee, "Early exit from a yearly contract");

        _audit.Staff(by, "member.cancel_requested", m.Id,
            $"Cancels on {m.CancelEffectiveOn:yyyy-MM-dd} ({GymRules.CancellationNoticeDays} days' notice).{(exitFee ? $" Early-exit fee ${GymRules.YearlyEarlyExitFee:0} charged." : "")} Reason: {reason}");
        await _db.SaveChangesAsync();
    }

    /// <summary>Take back a cancellation before it takes effect. An early-exit fee already paid isn't refunded here.</summary>
    public async Task UndoCancelAsync(Staff by, int memberId)
    {
        by.Require(Permission.CancelMembership);
        var m = await GetAsync(memberId);
        if (m.Status == MemberStatus.Cancelled) throw new GymRuleException("already_cancelled", "The cancellation already took effect. Sign them up again instead.");
        if (m.CancelEffectiveOn is null) throw new GymRuleException("not_cancelling", "This membership isn't being cancelled.");
        m.CancelRequestedOn = null;
        m.CancelEffectiveOn = null;
        _audit.Staff(by, "member.cancel_undone", m.Id, "Cancellation taken back.");
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Put a promo code on the membership. Codes don't stack with student or corporate discounts.
    /// </summary>
    public async Task ApplyPromoAsync(Staff by, int memberId, string code)
    {
        by.Require(Permission.EditMembers);
        var m = await GetAsync(memberId);
        if (m.Discount != DiscountType.None)
            throw new GymRuleException("discount_no_stack", $"Promo codes don't stack with the {m.Discount.ToString().ToLower()} discount {m.FirstName} already has.");
        var promo = await _db.PromoCodes.FirstOrDefaultAsync(p => p.Code == code.Trim().ToUpper())
            ?? throw new GymRuleException("unknown_code", "There's no promo code with that name.");
        if (!promo.Active || promo.ValidUntil < _clock.Today)
            throw new GymRuleException("expired_code", $"{promo.Code} expired on {promo.ValidUntil:yyyy-MM-dd}.");

        m.PromoCodeId = promo.Id;
        m.PromoMonthsLeft = promo.Months;
        _audit.Staff(by, "promo.applied", m.Id, $"{promo.Code}: {promo.PercentOff:0}% off the next {promo.Months} bill(s).");
        await _db.SaveChangesAsync();
    }

    public async Task UpdateDetailsAsync(Staff by, int memberId, string phone, int homeLocationId, string notes)
    {
        by.Require(Permission.EditMembers);
        var m = await GetAsync(memberId);
        var changes = new List<string>();
        if (m.Phone != phone) changes.Add("phone");
        if (m.HomeLocationId != homeLocationId) changes.Add("home location");
        if (m.Notes != notes) changes.Add("notes");
        m.Phone = phone; m.HomeLocationId = homeLocationId; m.Notes = notes;
        if (changes.Count > 0) _audit.Staff(by, "member.updated", m.Id, $"Changed {string.Join(", ", changes)}.");
        await _db.SaveChangesAsync();
    }

    public static DateOnly NextCycle(DateOnly from, BillingCycle cycle) =>
        cycle == BillingCycle.Yearly ? from.AddYears(1) : from.AddMonths(1);
}
