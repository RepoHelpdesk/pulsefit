namespace PulseFit.Api.Domain;

/// <summary>
/// The house rules every PulseFit gym runs on. Services read them from here, so a rule is changed in
/// one place. Money is in US dollars.
/// </summary>
public static class GymRules
{
    // ---- Classes ---------------------------------------------------------------
    /// <summary>Members can book a class this many days before it starts, and no earlier.</summary>
    public const int BookingOpensDaysAhead = 7;
    /// <summary>Cancelling less than this long before a class starts is a late cancel.</summary>
    public static readonly TimeSpan LateCancelWindow = TimeSpan.FromHours(4);
    public const decimal LateCancelFee = 10m;
    public const decimal NoShowFee = 10m;
    /// <summary>This many no-shows inside <see cref="NoShowLookbackDays"/> blocks booking for <see cref="NoShowBlockDays"/>.</summary>
    public const int NoShowsBeforeBlock = 3;
    public const int NoShowLookbackDays = 30;
    public const int NoShowBlockDays = 7;

    // ---- Freezing --------------------------------------------------------------
    /// <summary>A member may freeze for at most this many months in a calendar year.</summary>
    public const int MaxFreezeMonthsPerYear = 2;
    /// <summary>Charged for each month a membership is frozen, instead of the plan price.</summary>
    public const decimal FreezeFeePerMonth = 5m;

    // ---- Cancelling ------------------------------------------------------------
    /// <summary>A cancellation takes effect this many days after it is asked for.</summary>
    public const int CancellationNoticeDays = 30;
    /// <summary>Charged when a yearly plan is cancelled before its contract ends.</summary>
    public const decimal YearlyEarlyExitFee = 75m;

    // ---- Billing ---------------------------------------------------------------
    /// <summary>After a failed payment the member keeps access this many days, then is suspended.</summary>
    public const int PaymentGraceDays = 5;
    public const decimal StudentDiscountPercent = 20m;
    public const decimal CorporateDiscountPercent = 15m;

    // ---- Front desk ------------------------------------------------------------
    public const decimal DayPassPrice = 15m;
    /// <summary>Front desk staff may waive one fee per member in this many days; managers have no limit.</summary>
    public const int FrontDeskWaiverCooldownDays = 90;

    // ---- Refunds ---------------------------------------------------------------
    /// <summary>Managers may refund up to this much on their own; above it the owner must approve.</summary>
    public const decimal ManagerRefundLimit = 100m;
}
