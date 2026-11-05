using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Auth;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;
using PulseFit.Api.Dtos;
using PulseFit.Api.Jobs;
using PulseFit.Api.Queries;
using PulseFit.Api.Services;

namespace PulseFit.Api.Controllers;

/// <summary>The gym itself: locations, plans, staff, reports, the audit log and background jobs.</summary>
[ApiController, Authorize]
[Route("api")]
public class GymController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly StaffContext _staff;
    private readonly ReportQueries _reports;
    private readonly AuditService _audit;

    public GymController(GymDbContext db, StaffContext staff, ReportQueries reports, AuditService audit)
    {
        _db = db; _staff = staff; _reports = reports; _audit = audit;
    }

    [HttpGet("locations")]
    public async Task<IEnumerable<LocationDto>> Locations() =>
        (await _db.Locations.OrderBy(l => l.Id).ToListAsync()).Select(l => l.ToDto());

    [HttpGet("plans")]
    public async Task<IEnumerable<PlanDto>> Plans() =>
        (await _db.Plans.OrderBy(p => p.MonthlyPrice).ToListAsync()).Select(p => p.ToDto());

    /// <summary>New prices apply from each member's next bill; nobody is charged again for the current cycle.</summary>
    [HttpPut("plans/{id:int}/price")]
    public async Task<PlanDto> SetPrice(int id, PlanPriceRequest r)
    {
        var by = await _staff.CurrentAsync();
        by.Require(Permission.ManagePlans);
        var plan = await _db.Plans.FindAsync(id) ?? throw new NotFoundException("Plan");
        if (r.MonthlyPrice <= 0 || r.YearlyPrice <= 0) throw new GymRuleException("bad_price", "Prices must be above zero.");
        _audit.Staff(by, "plan.price_changed", null, $"{plan.Name}: ${plan.MonthlyPrice:0.00}/mo → ${r.MonthlyPrice:0.00}/mo, ${plan.YearlyPrice:0.00}/yr → ${r.YearlyPrice:0.00}/yr.");
        plan.MonthlyPrice = r.MonthlyPrice;
        plan.YearlyPrice = r.YearlyPrice;
        await _db.SaveChangesAsync();
        return plan.ToDto();
    }

    [HttpGet("promo-codes")]
    public async Task<IEnumerable<PromoCodeDto>> PromoCodes() =>
        (await _db.PromoCodes.OrderByDescending(p => p.ValidUntil).ToListAsync()).Select(p => p.ToDto());

    [HttpGet("staff")]
    public async Task<IEnumerable<StaffDto>> Staff() =>
        (await _db.Staff.Include(s => s.Location).OrderBy(s => s.Role).ThenBy(s => s.Name).ToListAsync()).Select(s => s.ToDto());

    [HttpGet("reports/summary")]
    public async Task<GymReport> Report()
    {
        (await _staff.CurrentAsync()).Require(Permission.ViewReports);
        return await _reports.SummaryAsync();
    }

    [HttpGet("audit")]
    public async Task<Paged<AuditDto>> Audit(int? memberId, string? actor, string? action, int skip = 0, int take = 50)
    {
        (await _staff.CurrentAsync()).Require(Permission.ViewAudit);
        return await _reports.AuditAsync(memberId, actor, action, skip, take);
    }

    [HttpGet("jobs")]
    public async Task<IEnumerable<JobDto>> Jobs([FromServices] JobRunner runner)
    {
        (await _staff.CurrentAsync()).Require(Permission.RunJobs);
        return (await runner.ListAsync()).Select(j => new JobDto(j.Name, j.Description, j.LastRun?.StartedAt, j.LastRun?.Summary, j.LastRun?.Failed ?? false));
    }

    [HttpPost("jobs/{name}/run")]
    public async Task<JobDto> RunJob(string name, [FromServices] JobRunner runner, CancellationToken ct)
    {
        var by = await _staff.CurrentAsync();
        by.Require(Permission.RunJobs);
        var run = await runner.RunAsync(name, ct);
        var job = (await runner.ListAsync()).First(j => j.Name == name);
        return new JobDto(job.Name, job.Description, run.StartedAt, run.Summary, run.Failed);
    }
}

/// <summary>What the page needs before sign-in: the gym's name and the RepoHelpdesk links.</summary>
[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public AppConfigDto Get([FromServices] IConfiguration config) => new(
        config["Gym:Name"] ?? "PulseFit",
        NullIfEmpty(config["RepoHelpdesk:WidgetScriptUrl"]),
        NullIfEmpty(config["RepoHelpdesk:WidgetKey"]),
        NullIfEmpty(config["RepoHelpdesk:DashboardUrl"]),
        NullIfEmpty(config["RepoHelpdesk:RepoUrl"]));

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
