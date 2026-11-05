using PulseFit.Api.Data;
using PulseFit.Api.Domain;

namespace PulseFit.Api.Services;

/// <summary>Records who did what. Added to the same unit of work as the change, so both save or neither.</summary>
public class AuditService
{
    private readonly GymDbContext _db;
    private readonly GymClock _clock;

    public AuditService(GymDbContext db, GymClock clock) { _db = db; _clock = clock; }

    public void Staff(Staff by, string action, int? memberId, string details) =>
        _db.Audit.Add(new AuditEntry { At = _clock.UtcNow, Actor = $"{by.Name} ({by.Role})", StaffId = by.Id, Action = action, MemberId = memberId, Details = details });

    public void System(string job, string action, int? memberId, string details) =>
        _db.Audit.Add(new AuditEntry { At = _clock.UtcNow, Actor = $"system:{job}", Action = action, MemberId = memberId, Details = details });
}
