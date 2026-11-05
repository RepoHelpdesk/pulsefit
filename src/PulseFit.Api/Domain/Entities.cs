namespace PulseFit.Api.Domain;

public class Location
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    /// <summary>Opening hours in the gym's local time.</summary>
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
}

public class Plan
{
    public int Id { get; set; }
    public PlanTier Tier { get; set; }
    public string Name { get; set; } = "";
    public decimal MonthlyPrice { get; set; }
    /// <summary>Paid once a year, up front.</summary>
    public decimal YearlyPrice { get; set; }
    /// <summary>False: the member may only check in at their home location.</summary>
    public bool AllLocations { get; set; }
    /// <summary>Classes a member may book each calendar month; null is unlimited, 0 is none.</summary>
    public int? ClassesPerMonth { get; set; }
    /// <summary>Guest passes each calendar month. Unused ones don't roll over.</summary>
    public int GuestPassesPerMonth { get; set; }
    public bool Active { get; set; } = true;

    public decimal PriceFor(BillingCycle cycle) => cycle == BillingCycle.Yearly ? YearlyPrice : MonthlyPrice;
}

public class PromoCode
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public decimal PercentOff { get; set; }
    /// <summary>How many billing runs the discount lasts.</summary>
    public int Months { get; set; }
    public DateOnly ValidUntil { get; set; }
    public bool Active { get; set; } = true;
}

public class Member
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public int HomeLocationId { get; set; }
    public Location? HomeLocation { get; set; }
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }
    public BillingCycle BillingCycle { get; set; }
    public MemberStatus Status { get; set; }
    public DateOnly JoinedOn { get; set; }
    public DateOnly NextBillingDate { get; set; }
    /// <summary>Yearly plans only: when the year the member signed up for ends.</summary>
    public DateOnly? ContractEndsOn { get; set; }

    /// <summary>A downgrade waiting for the next billing date.</summary>
    public int? PendingPlanId { get; set; }

    public DiscountType Discount { get; set; }
    public int? PromoCodeId { get; set; }
    public PromoCode? PromoCode { get; set; }
    /// <summary>Billing runs the promo code still applies to.</summary>
    public int PromoMonthsLeft { get; set; }

    public CardStatus CardStatus { get; set; }
    public string CardLast4 { get; set; } = "";

    public DateOnly? FreezeFrom { get; set; }
    public DateOnly? FreezeUntil { get; set; }
    public int FreezeYear { get; set; }
    public int FreezeMonthsUsed { get; set; }

    public DateOnly? PastDueSince { get; set; }
    public DateOnly? CancelRequestedOn { get; set; }
    public DateOnly? CancelEffectiveOn { get; set; }
    public DateTime? BookingBlockedUntil { get; set; }

    public string Notes { get; set; } = "";

    public string FullName => $"{FirstName} {LastName}";
}

public class Staff
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public StaffRole Role { get; set; }
    /// <summary>Null for staff who work across every location (the owner).</summary>
    public int? LocationId { get; set; }
    public Location? Location { get; set; }
}

public class ClassSession
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int TrainerId { get; set; }
    public Staff? Trainer { get; set; }
    public int LocationId { get; set; }
    public Location? Location { get; set; }
    public string Room { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public int DurationMinutes { get; set; }
    public int Capacity { get; set; }
    public bool Cancelled { get; set; }

    public DateTime EndsAt => StartsAt.AddMinutes(DurationMinutes);
}

public class Booking
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public ClassSession? Session { get; set; }
    public int MemberId { get; set; }
    public Member? Member { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    /// <summary>When a waitlisted booking got a place.</summary>
    public DateTime? PromotedAt { get; set; }
}

public class CheckIn
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public Member? Member { get; set; }
    public int LocationId { get; set; }
    public Location? Location { get; set; }
    public DateTime At { get; set; }
    public bool Allowed { get; set; }
    /// <summary>Why the desk turned the member away, when it did.</summary>
    public string? DeniedReason { get; set; }
    public int? StaffId { get; set; }
}

public class GuestPass
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public string GuestName { get; set; } = "";
    public int LocationId { get; set; }
    public DateTime UsedAt { get; set; }
}

public class Invoice
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public Member? Member { get; set; }
    public InvoiceKind Kind { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal RefundedAmount { get; set; }
    public InvoiceStatus Status { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? FailureReason { get; set; }
    public int? WaivedByStaffId { get; set; }
    public DateTime? WaivedAt { get; set; }
}

public class Refund
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public RefundStatus Status { get; set; }
    public int RequestedByStaffId { get; set; }
    public Staff? RequestedBy { get; set; }
    public int? DecidedByStaffId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
}

/// <summary>Who did what. Staff actions name the staff member; background jobs name the job.</summary>
public class AuditEntry
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    public string Actor { get; set; } = "";
    public int? StaffId { get; set; }
    public string Action { get; set; } = "";
    public int? MemberId { get; set; }
    public string Details { get; set; } = "";
}

/// <summary>The last time each background job ran, and what it did.</summary>
public class JobRun
{
    public int Id { get; set; }
    public string Job { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Summary { get; set; } = "";
    public bool Failed { get; set; }
}
