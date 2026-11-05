using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PulseFit.Api.Auth;
using PulseFit.Api.Dtos;
using PulseFit.Api.Queries;
using PulseFit.Api.Services;

namespace PulseFit.Api.Controllers;

[ApiController, Authorize]
[Route("api/classes")]
public class ClassesController : ControllerBase
{
    private readonly StaffContext _staff;
    private readonly ScheduleQueries _queries;
    private readonly BookingService _bookings;

    public ClassesController(StaffContext staff, ScheduleQueries queries, BookingService bookings)
    {
        _staff = staff; _queries = queries; _bookings = bookings;
    }

    /// <summary>The schedule between two instants (UTC). "mine" limits a trainer to their own classes.</summary>
    [HttpGet]
    public async Task<List<SessionRow>> List(DateTime from, DateTime to, int? locationId, bool mine = false)
    {
        var me = await _staff.CurrentAsync();
        return await _queries.ListAsync(from.ToUniversalTime(), to.ToUniversalTime(), locationId, mine ? me.Id : null);
    }

    [HttpGet("{id:int}")]
    public async Task<SessionDetail> Get(int id)
    {
        await _staff.CurrentAsync();
        return await _queries.GetAsync(id);
    }

    [HttpPost("{id:int}/bookings")]
    public async Task<SessionDetail> Book(int id, BookRequest r)
    {
        await _bookings.BookAsync(await _staff.CurrentAsync(), id, r.MemberId);
        return await _queries.GetAsync(id);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<SessionDetail> CancelSession(int id, CancelSessionRequest r)
    {
        await _bookings.CancelSessionAsync(await _staff.CurrentAsync(), id, r.Reason);
        return await _queries.GetAsync(id);
    }

    [HttpDelete("bookings/{bookingId:int}")]
    public async Task<IActionResult> CancelBooking(int bookingId)
    {
        await _bookings.CancelAsync(await _staff.CurrentAsync(), bookingId);
        return NoContent();
    }

    [HttpPost("bookings/{bookingId:int}/attendance")]
    public async Task<IActionResult> Attendance(int bookingId, AttendanceRequest r)
    {
        await _bookings.MarkAsync(await _staff.CurrentAsync(), bookingId, r.Attended);
        return NoContent();
    }
}
