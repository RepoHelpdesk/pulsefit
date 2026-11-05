using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PulseFit.Api.Auth;
using PulseFit.Api.Domain;
using PulseFit.Api.Dtos;
using PulseFit.Api.Queries;
using PulseFit.Api.Services;

namespace PulseFit.Api.Controllers;

[ApiController, Authorize]
[Route("api/members")]
public class MembersController : ControllerBase
{
    private readonly StaffContext _staff;
    private readonly MemberQueries _queries;
    private readonly MembershipService _memberships;
    private readonly BillingService _billing;

    public MembersController(StaffContext staff, MemberQueries queries, MembershipService memberships, BillingService billing)
    {
        _staff = staff; _queries = queries; _memberships = memberships; _billing = billing;
    }

    [HttpGet]
    public async Task<Paged<MemberRow>> Search(string? q, MemberStatus? status, int? planId, int? locationId, int skip = 0, int take = 25)
    {
        (await _staff.CurrentAsync()).Require(Permission.ViewMembers);
        return await _queries.SearchAsync(q, status, planId, locationId, skip, take);
    }

    [HttpGet("{id:int}")]
    public async Task<MemberDetail> Get(int id)
    {
        (await _staff.CurrentAsync()).Require(Permission.ViewMembers);
        return await _queries.GetAsync(id);
    }

    [HttpPost]
    public async Task<MemberDetail> Join(NewMemberRequest r)
    {
        var m = await _memberships.JoinAsync(await _staff.CurrentAsync(),
            new NewMember(r.FirstName, r.LastName, r.Email, r.Phone, r.HomeLocationId, r.PlanId, r.BillingCycle, r.Discount, r.CardLast4));
        return await _queries.GetAsync(m.Id);
    }

    [HttpPut("{id:int}")]
    public async Task<MemberDetail> Update(int id, UpdateMemberRequest r)
    {
        await _memberships.UpdateDetailsAsync(await _staff.CurrentAsync(), id, r.Phone, r.HomeLocationId, r.Notes);
        return await _queries.GetAsync(id);
    }

    [HttpPost("{id:int}/freeze")]
    public async Task<MemberDetail> Freeze(int id, FreezeRequest r)
    {
        await _memberships.FreezeAsync(await _staff.CurrentAsync(), id, r.Months);
        return await _queries.GetAsync(id);
    }

    [HttpPost("{id:int}/unfreeze")]
    public async Task<MemberDetail> Unfreeze(int id)
    {
        await _memberships.UnfreezeAsync(await _staff.CurrentAsync(), id);
        return await _queries.GetAsync(id);
    }

    [HttpPost("{id:int}/plan")]
    public async Task<MemberDetail> ChangePlan(int id, ChangePlanRequest r)
    {
        await _memberships.ChangePlanAsync(await _staff.CurrentAsync(), id, r.PlanId);
        return await _queries.GetAsync(id);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<MemberDetail> Cancel(int id, CancelRequest r)
    {
        await _memberships.CancelAsync(await _staff.CurrentAsync(), id, r.Reason);
        return await _queries.GetAsync(id);
    }

    [HttpPost("{id:int}/undo-cancel")]
    public async Task<MemberDetail> UndoCancel(int id)
    {
        await _memberships.UndoCancelAsync(await _staff.CurrentAsync(), id);
        return await _queries.GetAsync(id);
    }

    [HttpPost("{id:int}/promo")]
    public async Task<MemberDetail> Promo(int id, PromoRequest r)
    {
        await _memberships.ApplyPromoAsync(await _staff.CurrentAsync(), id, r.Code);
        return await _queries.GetAsync(id);
    }

    [HttpPost("{id:int}/card")]
    public async Task<MemberDetail> Card(int id, CardRequest r)
    {
        var by = await _staff.CurrentAsync();
        by.Require(Permission.EditMembers);
        await _billing.UpdateCardAsync(by, await _memberships.GetAsync(id), r.Last4);
        return await _queries.GetAsync(id);
    }
}
