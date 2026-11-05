using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;

namespace PulseFit.Api.Services;

/// <summary>
/// Charges, payments, fee waivers and refunds. The payment provider is simulated: a charge goes through
/// when the card on file is valid and fails when it is expired or declined.
/// </summary>
public class BillingService
{
    private readonly GymDbContext _db;
    private readonly GymClock _clock;
    private readonly AuditService _audit;

    public BillingService(GymDbContext db, GymClock clock, AuditService audit)
    {
        _db = db; _clock = clock; _audit = audit;
    }

    private static readonly InvoiceKind[] Fees =
        [InvoiceKind.LateCancelFee, InvoiceKind.NoShowFee, InvoiceKind.FreezeFee, InvoiceKind.EarlyExitFee];

    /// <summary>
    /// The plan price the member pays this cycle, after their discount. A student or corporate discount
    /// and a promo code never stack: a member with a discount can't hold a code (see
    /// <see cref="MembershipService.ApplyPromoAsync"/>), so only one of them is ever applied here.
    /// </summary>
    public decimal MembershipPrice(Member member, Plan plan)
    {
        var price = plan.PriceFor(member.BillingCycle);
        var percentOff = member.Discount switch
        {
            DiscountType.Student => GymRules.StudentDiscountPercent,
            DiscountType.Corporate => GymRules.CorporateDiscountPercent,
            _ => member.PromoCode is { } promo && member.PromoMonthsLeft > 0 ? promo.PercentOff : 0m,
        };
        return Math.Round(price * (100m - percentOff) / 100m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Bill the member and take payment from the card on file. A failed membership payment puts an
    /// active member past due, which starts the grace period.
    /// </summary>
    public Invoice Charge(Member member, InvoiceKind kind, decimal amount, string description)
    {
        var now = _clock.UtcNow;
        var invoice = new Invoice
        {
            MemberId = member.Id, Kind = kind, Amount = amount, Description = description, IssuedAt = now,
        };

        if (member.CardStatus == CardStatus.Valid)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidAt = now;
        }
        else
        {
            invoice.Status = InvoiceStatus.Failed;
            invoice.FailedAt = now;
            invoice.FailureReason = member.CardStatus == CardStatus.Expired ? "Card expired" : "Card declined";
            if (member.Status == MemberStatus.Active)
            {
                member.Status = MemberStatus.PastDue;
                member.PastDueSince = _clock.Today;
            }
        }

        _db.Invoices.Add(invoice);
        return invoice;
    }

    /// <summary>The member's unpaid failed charges.</summary>
    public Task<List<Invoice>> OutstandingAsync(int memberId) =>
        _db.Invoices.Where(i => i.MemberId == memberId && i.Status == InvoiceStatus.Failed).OrderBy(i => i.IssuedAt).ToListAsync();

    /// <summary>
    /// A new card was put on file: retry everything that failed. Once nothing is owed, a past-due or
    /// suspended member is active again.
    /// </summary>
    public async Task<int> UpdateCardAsync(Staff by, Member member, string last4)
    {
        member.CardStatus = CardStatus.Valid;
        member.CardLast4 = last4;
        var outstanding = await OutstandingAsync(member.Id);
        foreach (var invoice in outstanding)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidAt = _clock.UtcNow;
            invoice.FailureReason = null;
        }
        RestoreIfSettled(member, stillFailed: 0);
        _audit.Staff(by, "card.updated", member.Id, $"New card ending {last4}; {outstanding.Count} failed charge(s) collected.");
        await _db.SaveChangesAsync();
        return outstanding.Count;
    }

    /// <summary>
    /// Waive a fee. Only fees can be waived, never the membership itself. The front desk may waive one
    /// fee per member every <see cref="GymRules.FrontDeskWaiverCooldownDays"/> days; managers and the
    /// owner have no limit.
    /// </summary>
    public async Task WaiveAsync(Staff by, int invoiceId, string reason)
    {
        by.Require(Permission.WaiveFee);
        var invoice = await _db.Invoices.Include(i => i.Member).FirstOrDefaultAsync(i => i.Id == invoiceId)
            ?? throw new NotFoundException("Invoice");
        if (!Fees.Contains(invoice.Kind))
            throw new GymRuleException("not_a_fee", "Only fees can be waived. For a membership charge, issue a refund.");
        if (invoice.Status is not (InvoiceStatus.Paid or InvoiceStatus.Failed or InvoiceStatus.Open))
            throw new GymRuleException("not_waivable", $"This charge is already {invoice.Status.ToString().ToLower()}.");

        if (by.Role == StaffRole.FrontDesk)
        {
            var since = _clock.UtcNow.AddDays(-GymRules.FrontDeskWaiverCooldownDays);
            var frontDeskIds = _db.Staff.Where(s => s.Role == StaffRole.FrontDesk).Select(s => s.Id);
            var recent = await _db.Invoices.AnyAsync(i => i.MemberId == invoice.MemberId && i.WaivedAt >= since
                && i.WaivedByStaffId != null && frontDeskIds.Contains(i.WaivedByStaffId.Value));
            if (recent)
                throw new GymRuleException("waiver_cooldown",
                    $"The front desk can waive one fee per member every {GymRules.FrontDeskWaiverCooldownDays} days. Ask a manager.");
        }

        var wasFailed = invoice.Status == InvoiceStatus.Failed;
        invoice.Status = InvoiceStatus.Waived;
        invoice.WaivedByStaffId = by.Id;
        invoice.WaivedAt = _clock.UtcNow;
        if (wasFailed) RestoreIfSettled(invoice.Member!, await _db.Invoices.CountAsync(i =>
            i.MemberId == invoice.MemberId && i.Status == InvoiceStatus.Failed && i.Id != invoice.Id));

        _audit.Staff(by, "fee.waived", invoice.MemberId, $"Waived {invoice.Description} (${invoice.Amount:0.00}). Reason: {reason}");
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Refund a paid charge, in full or in part. A manager's refund up to
    /// <see cref="GymRules.ManagerRefundLimit"/> goes through at once; a bigger one waits for the
    /// owner. The owner's own refunds always go through.
    /// </summary>
    public async Task<Refund> RequestRefundAsync(Staff by, int invoiceId, decimal amount, string reason)
    {
        if (!by.Can(Permission.RequestRefund)) throw new GymRuleException("forbidden", "Your role can't issue refunds. Ask a manager.");
        var invoice = await _db.Invoices.FindAsync(invoiceId) ?? throw new NotFoundException("Invoice");
        if (invoice.Status is not (InvoiceStatus.Paid or InvoiceStatus.PartiallyRefunded))
            throw new GymRuleException("not_refundable", "Only a paid charge can be refunded.");
        var pending = (await _db.Refunds.Where(r => r.InvoiceId == invoiceId && r.Status == RefundStatus.Pending).Select(r => r.Amount).ToListAsync()).Sum();
        var refundable = invoice.Amount - invoice.RefundedAmount - pending;
        if (amount <= 0 || amount > refundable)
            throw new GymRuleException("bad_amount", $"You can refund up to ${refundable:0.00} of this charge.");

        var refund = new Refund
        {
            InvoiceId = invoiceId, Amount = amount, Reason = reason, RequestedByStaffId = by.Id, CreatedAt = _clock.UtcNow,
            Status = RefundStatus.Pending,
        };
        _db.Refunds.Add(refund);

        if (by.Role == StaffRole.Owner || amount <= GymRules.ManagerRefundLimit)
        {
            Apply(refund, invoice, by);
            _audit.Staff(by, "refund.issued", invoice.MemberId, $"Refunded ${amount:0.00} of {invoice.Description}. Reason: {reason}");
        }
        else
        {
            _audit.Staff(by, "refund.requested", invoice.MemberId,
                $"Asked the owner to approve a ${amount:0.00} refund of {invoice.Description} (over the ${GymRules.ManagerRefundLimit:0} manager limit). Reason: {reason}");
        }
        await _db.SaveChangesAsync();
        return refund;
    }

    public async Task DecideRefundAsync(Staff by, int refundId, bool approve)
    {
        if (!by.Can(Permission.ApproveRefund)) throw new GymRuleException("forbidden", "Only the owner can approve refunds.");
        var refund = await _db.Refunds.Include(r => r.Invoice).FirstOrDefaultAsync(r => r.Id == refundId)
            ?? throw new NotFoundException("Refund");
        if (refund.Status != RefundStatus.Pending) throw new GymRuleException("decided", "This refund was already decided.");

        if (approve) Apply(refund, refund.Invoice!, by);
        else
        {
            refund.Status = RefundStatus.Rejected;
            refund.DecidedByStaffId = by.Id;
            refund.DecidedAt = _clock.UtcNow;
        }
        _audit.Staff(by, approve ? "refund.approved" : "refund.rejected", refund.Invoice!.MemberId,
            $"{(approve ? "Approved" : "Rejected")} a ${refund.Amount:0.00} refund of {refund.Invoice.Description}.");
        await _db.SaveChangesAsync();
    }

    private void Apply(Refund refund, Invoice invoice, Staff by)
    {
        refund.Status = RefundStatus.Approved;
        refund.DecidedByStaffId = by.Id;
        refund.DecidedAt = _clock.UtcNow;
        invoice.RefundedAmount += refund.Amount;
        invoice.Status = invoice.RefundedAmount >= invoice.Amount ? InvoiceStatus.Refunded : InvoiceStatus.PartiallyRefunded;
    }

    /// <summary>Past due or suspended, and nothing left owing: back to active.</summary>
    private static void RestoreIfSettled(Member member, int stillFailed)
    {
        if (stillFailed > 0) return;
        if (member.Status is MemberStatus.PastDue or MemberStatus.Suspended)
        {
            member.Status = MemberStatus.Active;
            member.PastDueSince = null;
        }
    }
}
