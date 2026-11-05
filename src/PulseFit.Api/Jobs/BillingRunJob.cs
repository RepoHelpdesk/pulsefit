using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;
using PulseFit.Api.Services;

namespace PulseFit.Api.Jobs;

/// <summary>
/// Bills every membership whose billing date has come: the plan price after discounts, or the freeze
/// fee while frozen. A downgrade waiting for this date happens first, so the new plan is what's billed.
/// </summary>
public class BillingRunJob : IGymJob
{
    private readonly GymDbContext _db;
    private readonly GymClock _clock;
    private readonly BillingService _billing;
    private readonly AuditService _audit;

    public BillingRunJob(GymDbContext db, GymClock clock, BillingService billing, AuditService audit)
    {
        _db = db; _clock = clock; _billing = billing; _audit = audit;
    }

    public string Name => "billing-run";
    public string Description => "Bills memberships whose billing date has come.";

    public async Task<string> RunAsync(CancellationToken ct)
    {
        var today = _clock.Today;
        var due = await _db.Members.Include(m => m.Plan).Include(m => m.PromoCode)
            .Where(m => m.Status != MemberStatus.Cancelled && m.Status != MemberStatus.Suspended && m.NextBillingDate <= today)
            .ToListAsync(ct);

        int billed = 0, failed = 0, skipped = 0;
        foreach (var m in due)
        {
            // A member who is leaving is billed for every cycle that starts before they go.
            if (m.CancelEffectiveOn is { } leaving && m.NextBillingDate > leaving)
            {
                skipped++;
                continue;
            }

            if (m.PendingPlanId is { } newPlanId)
            {
                var newPlan = await _db.Plans.FindAsync([newPlanId], ct);
                _audit.System(Name, "plan.downgraded", m.Id, $"Moved {m.Plan!.Name} → {newPlan!.Name} on the billing date.");
                m.PlanId = newPlan.Id;
                m.Plan = newPlan;
                m.PendingPlanId = null;
            }

            Invoice invoice;
            if (m.Status == MemberStatus.Frozen)
            {
                invoice = _billing.Charge(m, InvoiceKind.FreezeFee, GymRules.FreezeFeePerMonth,
                    $"Freeze fee, {m.NextBillingDate:MMM d} – {m.NextBillingDate.AddMonths(1):MMM d}");
            }
            else
            {
                var price = _billing.MembershipPrice(m, m.Plan!);
                var period = $"{m.NextBillingDate:MMM d, yyyy} – {MembershipService.NextCycle(m.NextBillingDate, m.BillingCycle):MMM d, yyyy}";
                invoice = _billing.Charge(m, InvoiceKind.Membership, price, $"{m.Plan!.Name} ({m.BillingCycle.ToString().ToLower()}), {period}");
                if (m.Discount == DiscountType.None && m.PromoMonthsLeft > 0) m.PromoMonthsLeft--;
            }

            m.NextBillingDate = m.Status == MemberStatus.Frozen
                ? m.NextBillingDate.AddMonths(1)
                : MembershipService.NextCycle(m.NextBillingDate, m.BillingCycle);
            if (invoice.Status == InvoiceStatus.Failed) failed++; else billed++;
            _audit.System(Name, invoice.Status == InvoiceStatus.Failed ? "billing.failed" : "billing.charged", m.Id,
                $"{invoice.Description}: ${invoice.Amount:0.00}{(invoice.FailureReason is null ? "" : $" — {invoice.FailureReason}")}. Next bill {m.NextBillingDate:yyyy-MM-dd}.");
        }

        await _db.SaveChangesAsync(ct);
        return $"{billed} charged, {failed} failed, {skipped} skipped (leaving before their billing date).";
    }
}
