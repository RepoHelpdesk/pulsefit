using PulseFit.Api.Domain;
using PulseFit.Api.Dtos;

namespace PulseFit.Api.Queries;

public static class Mapping
{
    public static PlanDto ToDto(this Plan p) =>
        new(p.Id, p.Tier, p.Name, p.MonthlyPrice, p.YearlyPrice, p.AllLocations, p.ClassesPerMonth, p.GuestPassesPerMonth, p.Active);

    public static StaffDto ToDto(this Staff s) => new(s.Id, s.Name, s.Email, s.Role, s.Location?.Name);

    public static LocationDto ToDto(this Location l) => new(l.Id, l.Name, l.Address, l.OpensAt, l.ClosesAt);

    public static PromoCodeDto ToDto(this PromoCode p) => new(p.Id, p.Code, p.PercentOff, p.Months, p.ValidUntil, p.Active);

    public static InvoiceDto ToDto(this Invoice i) =>
        new(i.Id, i.MemberId, i.Member?.FullName ?? "", i.Kind, i.Description, i.Amount, i.RefundedAmount, i.Status, i.IssuedAt, i.FailureReason);
}
