using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Data;
using PulseFit.Api.Domain;
using PulseFit.Api.Dtos;
using PulseFit.Api.Services;

namespace PulseFit.Api.Queries;

public class ScheduleQueries
{
    private readonly GymDbContext _db;
    public ScheduleQueries(GymDbContext db) { _db = db; }

    private IQueryable<SessionRow> Rows(IQueryable<ClassSession> q) => q.Select(s => new SessionRow(
        s.Id, s.Name, s.Trainer!.Name, s.TrainerId, s.Location!.Name, s.LocationId, s.Room, s.StartsAt, s.DurationMinutes, s.Capacity,
        _db.Bookings.Count(b => b.SessionId == s.Id && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Attended)),
        _db.Bookings.Count(b => b.SessionId == s.Id && b.Status == BookingStatus.Waitlisted),
        s.Cancelled));

    /// <summary>Classes starting between two instants, optionally at one location or with one trainer.</summary>
    public Task<List<SessionRow>> ListAsync(DateTime fromUtc, DateTime toUtc, int? locationId, int? trainerId)
    {
        var q = _db.Sessions.Where(s => s.StartsAt >= fromUtc && s.StartsAt < toUtc);
        if (locationId is not null) q = q.Where(s => s.LocationId == locationId);
        if (trainerId is not null) q = q.Where(s => s.TrainerId == trainerId);
        return Rows(q.OrderBy(s => s.StartsAt)).ToListAsync();
    }

    public async Task<SessionDetail> GetAsync(int id)
    {
        var row = await Rows(_db.Sessions.Where(s => s.Id == id)).FirstOrDefaultAsync() ?? throw new NotFoundException("Class");
        var roster = await _db.Bookings.Where(b => b.SessionId == id)
            .OrderBy(b => b.Status == BookingStatus.Waitlisted).ThenBy(b => b.CreatedAt)
            .Select(b => new RosterEntry(b.Id, b.MemberId, b.Member!.FirstName + " " + b.Member.LastName, b.Member.Plan!.Name, b.Status, b.CreatedAt, b.PromotedAt))
            .ToListAsync();
        return new SessionDetail(row, roster);
    }
}
