using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;

namespace PulseFit.Api.Auth;

/// <summary>
/// Who is signed in. This is a demo, so there are no passwords: anyone picks a staff member on the
/// sign-in screen and works as them. Swap <see cref="SignInAsync"/> for real sign-in and nothing else
/// changes — every check downstream goes through the staff member's role.
/// </summary>
public class StaffContext
{
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;
    private const string StaffIdClaim = "staff_id";

    private readonly IHttpContextAccessor _http;
    private readonly GymDbContext _db;
    private Staff? _current;

    public StaffContext(IHttpContextAccessor http, GymDbContext db) { _http = http; _db = db; }

    /// <summary>The signed-in staff member. Endpoints that call this require sign-in, so it's never missing there.</summary>
    public async Task<Staff> CurrentAsync()
    {
        if (_current is not null) return _current;
        var id = _http.HttpContext?.User.FindFirstValue(StaffIdClaim);
        _current = id is null ? null : await _db.Staff.FindAsync(int.Parse(id));
        return _current ?? throw new UnauthorizedAccessException();
    }

    public async Task SignInAsync(Staff staff)
    {
        var identity = new ClaimsIdentity(
            [new Claim(StaffIdClaim, staff.Id.ToString()), new Claim(ClaimTypes.Name, staff.Name), new Claim(ClaimTypes.Role, staff.Role.ToString())],
            Scheme);
        await _http.HttpContext!.SignInAsync(Scheme, new ClaimsPrincipal(identity));
    }

    public Task SignOutAsync() => _http.HttpContext!.SignOutAsync(Scheme);
}
