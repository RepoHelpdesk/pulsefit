using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Auth;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;
using PulseFit.Api.Dtos;
using PulseFit.Api.Queries;

namespace PulseFit.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly StaffContext _staff;

    public AuthController(GymDbContext db, StaffContext staff) { _db = db; _staff = staff; }

    /// <summary>Everyone who can be signed in as, for the demo sign-in screen.</summary>
    [HttpGet("staff"), AllowAnonymous]
    public async Task<IEnumerable<StaffDto>> Staff() =>
        (await _db.Staff.Include(s => s.Location).OrderBy(s => s.Role).ThenBy(s => s.Name).ToListAsync()).Select(s => s.ToDto());

    [HttpPost("sign-in"), AllowAnonymous]
    public async Task<ActionResult<MeDto>> SignIn(SignInRequest req)
    {
        var staff = await _db.Staff.Include(s => s.Location).FirstOrDefaultAsync(s => s.Id == req.StaffId);
        if (staff is null) return NotFound();
        await _staff.SignInAsync(staff);
        return new MeDto(staff.ToDto(), Permissions.For(staff.Role));
    }

    [HttpPost("sign-out")]
    public async Task<IActionResult> SignOutStaff()
    {
        await _staff.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me"), Authorize]
    public async Task<MeDto> Me()
    {
        var staff = await _staff.CurrentAsync();
        await _db.Entry(staff).Reference(s => s.Location).LoadAsync();
        return new MeDto(staff.ToDto(), Permissions.For(staff.Role));
    }
}
