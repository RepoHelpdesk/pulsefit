namespace PulseFit.Api.Domain;

public enum PlanTier { Basic, Plus, Premium }

public enum BillingCycle { Monthly, Yearly }

public enum MemberStatus
{
    Active,
    /// <summary>Paused at the member's request; billed the freeze fee, can't check in.</summary>
    Frozen,
    /// <summary>A payment failed; still has access until the grace period ends.</summary>
    PastDue,
    /// <summary>The grace period ran out without payment; no access until the balance is paid.</summary>
    Suspended,
    Cancelled,
}

public enum DiscountType { None, Student, Corporate }

/// <summary>The state of the card on file, as the payment provider last reported it.</summary>
public enum CardStatus { Valid, Expired, Declined }

public enum StaffRole { FrontDesk, Trainer, Manager, Owner }

public enum BookingStatus { Booked, Waitlisted, Cancelled, LateCancelled, Attended, NoShow }

public enum InvoiceKind { Membership, Proration, LateCancelFee, NoShowFee, FreezeFee, EarlyExitFee, DayPass }

public enum InvoiceStatus { Open, Paid, Failed, Waived, Refunded, PartiallyRefunded }

public enum RefundStatus { Pending, Approved, Rejected }
