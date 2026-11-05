using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PulseFit.Api.Auth;
using PulseFit.Api.Domain;
using PulseFit.Api.Dtos;
using PulseFit.Api.Queries;
using PulseFit.Api.Services;

namespace PulseFit.Api.Controllers;

[ApiController, Authorize]
[Route("api/front-desk")]
public class FrontDeskController : ControllerBase
{
    private readonly StaffContext _staff;
    private readonly CheckInService _checkIns;
    private readonly ReportQueries _reports;

    public FrontDeskController(StaffContext staff, CheckInService checkIns, ReportQueries reports)
    {
        _staff = staff; _checkIns = checkIns; _reports = reports;
    }

    [HttpPost("check-in")]
    public async Task<CheckInResponse> CheckIn(CheckInRequest r)
    {
        var result = await _checkIns.CheckInAsync(await _staff.CurrentAsync(), r.MemberId, r.LocationId);
        return new CheckInResponse(result.Allowed, result.Message, result.ClassesMarked);
    }

    [HttpPost("guest")]
    public async Task<object> Guest(GuestRequest r) =>
        new { message = await _checkIns.GuestAsync(await _staff.CurrentAsync(), r.MemberId, r.LocationId, r.GuestName, r.BuyDayPass) };

    [HttpGet("recent")]
    public async Task<List<CheckInDto>> Recent(int? locationId, int take = 30)
    {
        (await _staff.CurrentAsync()).Require(Permission.CheckIn);
        return await _reports.RecentCheckInsAsync(locationId, Math.Clamp(take, 1, 100));
    }
}
