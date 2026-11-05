using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PulseFit.Api.Auth;
using PulseFit.Api.Domain;
using PulseFit.Api.Dtos;
using PulseFit.Api.Queries;
using PulseFit.Api.Services;

namespace PulseFit.Api.Controllers;

[ApiController, Authorize]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly StaffContext _staff;
    private readonly BillingQueries _queries;
    private readonly BillingService _billing;

    public BillingController(StaffContext staff, BillingQueries queries, BillingService billing)
    {
        _staff = staff; _queries = queries; _billing = billing;
    }

    [HttpGet("invoices")]
    public async Task<Paged<InvoiceDto>> Invoices(InvoiceStatus? status, InvoiceKind? kind, int skip = 0, int take = 25)
    {
        (await _staff.CurrentAsync()).Require(Permission.ViewMembers);
        return await _queries.InvoicesAsync(status, kind, skip, take);
    }

    [HttpPost("invoices/{id:int}/waive")]
    public async Task<IActionResult> Waive(int id, WaiveRequest r)
    {
        await _billing.WaiveAsync(await _staff.CurrentAsync(), id, r.Reason);
        return NoContent();
    }

    [HttpPost("invoices/{id:int}/refund")]
    public async Task<object> Refund(int id, RefundRequest r)
    {
        var refund = await _billing.RequestRefundAsync(await _staff.CurrentAsync(), id, r.Amount, r.Reason);
        return new { refund.Id, refund.Status };
    }

    [HttpGet("refunds")]
    public async Task<List<RefundDto>> Refunds(RefundStatus? status)
    {
        (await _staff.CurrentAsync()).Require(Permission.RequestRefund);
        return await _queries.RefundsAsync(status);
    }

    [HttpPost("refunds/{id:int}/decide")]
    public async Task<IActionResult> Decide(int id, DecideRefundRequest r)
    {
        await _billing.DecideRefundAsync(await _staff.CurrentAsync(), id, r.Approve);
        return NoContent();
    }
}
