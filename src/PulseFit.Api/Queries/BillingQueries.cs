using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;
using PulseFit.Api.Dtos;

namespace PulseFit.Api.Queries;

public class BillingQueries
{
    private readonly GymDbContext _db;
    public BillingQueries(GymDbContext db) { _db = db; }

    public async Task<Paged<InvoiceDto>> InvoicesAsync(InvoiceStatus? status, InvoiceKind? kind, int skip, int take)
    {
        var q = _db.Invoices.Include(i => i.Member).AsQueryable();
        if (status is not null) q = q.Where(i => i.Status == status);
        if (kind is not null) q = q.Where(i => i.Kind == kind);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(i => i.IssuedAt).Skip(skip).Take(Math.Clamp(take, 1, 100)).ToListAsync();
        return new Paged<InvoiceDto>(items.Select(i => i.ToDto()).ToList(), total);
    }

    public Task<List<RefundDto>> RefundsAsync(RefundStatus? status) =>
        _db.Refunds.Where(r => status == null || r.Status == status).OrderByDescending(r => r.CreatedAt)
            .Select(r => new RefundDto(r.Id, r.InvoiceId, r.Invoice!.Description, r.Invoice.MemberId,
                r.Invoice.Member!.FirstName + " " + r.Invoice.Member.LastName, r.Amount, r.Reason, r.Status,
                r.RequestedBy!.Name, r.CreatedAt, r.DecidedAt))
            .ToListAsync();
}
