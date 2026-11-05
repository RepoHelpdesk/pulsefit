using PulseFit.Api.Domain;

namespace PulseFit.Api.Dtos;

public record StaffDto(int Id, string Name, string Email, StaffRole Role, string? Location);

public record MeDto(StaffDto Staff, IEnumerable<Permission> Permissions);

public record SignInRequest(int StaffId);

public record PlanDto(int Id, PlanTier Tier, string Name, decimal MonthlyPrice, decimal YearlyPrice, bool AllLocations,
    int? ClassesPerMonth, int GuestPassesPerMonth, bool Active);

public record PlanPriceRequest(decimal MonthlyPrice, decimal YearlyPrice);

public record PromoCodeDto(int Id, string Code, decimal PercentOff, int Months, DateOnly ValidUntil, bool Active);

public record LocationDto(int Id, string Name, string Address, TimeOnly OpensAt, TimeOnly ClosesAt);

public record MemberRow(int Id, string Name, string Email, string Plan, BillingCycle BillingCycle, MemberStatus Status,
    string HomeLocation, DateOnly NextBillingDate, DateOnly? CancelEffectiveOn, bool BookingBlocked);

public record Paged<T>(IReadOnlyList<T> Items, int Total);

public record MemberDetail(
    int Id, string FirstName, string LastName, string Email, string Phone, int HomeLocationId, string HomeLocation,
    PlanDto Plan, PlanDto? PendingPlan, BillingCycle BillingCycle, MemberStatus Status, DateOnly JoinedOn,
    DateOnly NextBillingDate, DateOnly? ContractEndsOn, DiscountType Discount, string? PromoCode, int PromoMonthsLeft,
    CardStatus CardStatus, string CardLast4, DateOnly? FreezeUntil, int FreezeMonthsLeftThisYear, DateOnly? PastDueSince,
    DateOnly? CancelRequestedOn, DateOnly? CancelEffectiveOn, DateTime? BookingBlockedUntil, decimal Outstanding, string Notes,
    int ClassesThisMonth, int GuestPassesLeftThisMonth,
    IReadOnlyList<InvoiceDto> Invoices, IReadOnlyList<MemberBookingDto> Bookings, IReadOnlyList<CheckInDto> Visits,
    IReadOnlyList<AuditDto> Audit);

public record InvoiceDto(int Id, int MemberId, string MemberName, InvoiceKind Kind, string Description, decimal Amount,
    decimal RefundedAmount, InvoiceStatus Status, DateTime IssuedAt, string? FailureReason);

public record MemberBookingDto(int Id, int SessionId, string ClassName, DateTime StartsAt, string Location, BookingStatus Status, DateTime? PromotedAt);

public record CheckInDto(int Id, int MemberId, string MemberName, string Location, DateTime At, bool Allowed, string? DeniedReason);

public record AuditDto(long Id, DateTime At, string Actor, string Action, int? MemberId, string? MemberName, string Details);

public record NewMemberRequest(string FirstName, string LastName, string Email, string Phone, int HomeLocationId,
    int PlanId, BillingCycle BillingCycle, DiscountType Discount, string CardLast4);

public record UpdateMemberRequest(string Phone, int HomeLocationId, string Notes);
public record FreezeRequest(int Months);
public record ChangePlanRequest(int PlanId);
public record CancelRequest(string Reason);
public record PromoRequest(string Code);
public record CardRequest(string Last4);

public record SessionRow(int Id, string Name, string Trainer, int TrainerId, string Location, int LocationId, string Room,
    DateTime StartsAt, int DurationMinutes, int Capacity, int Booked, int Waitlisted, bool Cancelled);

public record RosterEntry(int BookingId, int MemberId, string MemberName, string Plan, BookingStatus Status, DateTime CreatedAt, DateTime? PromotedAt);

public record SessionDetail(SessionRow Session, IReadOnlyList<RosterEntry> Roster);

public record BookRequest(int MemberId);
public record AttendanceRequest(bool Attended);
public record CancelSessionRequest(string Reason);

public record CheckInRequest(int MemberId, int LocationId);
public record CheckInResponse(bool Allowed, string Message, int ClassesMarked);
public record GuestRequest(int MemberId, int LocationId, string GuestName, bool BuyDayPass);

public record WaiveRequest(string Reason);
public record RefundRequest(decimal Amount, string Reason);
public record RefundDto(int Id, int InvoiceId, string InvoiceDescription, int MemberId, string MemberName, decimal Amount,
    string Reason, RefundStatus Status, string RequestedBy, DateTime CreatedAt, DateTime? DecidedAt);
public record DecideRefundRequest(bool Approve);

public record JobDto(string Name, string Description, DateTime? LastRunAt, string? LastSummary, bool LastFailed);

/// <summary>What the page needs to show the RepoHelpdesk widget and its links. Empty values hide them.</summary>
public record AppConfigDto(string GymName, string? WidgetScriptUrl, string? WidgetKey, string? DashboardUrl, string? RepoUrl);
