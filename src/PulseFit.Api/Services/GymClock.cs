namespace PulseFit.Api.Services;

/// <summary>
/// The time, as the gyms see it. Everything is stored in UTC; dates that matter to members (billing
/// dates, cancellation dates) are the gym's local date.
/// </summary>
public class GymClock
{
    private readonly TimeZoneInfo _zone;

    public GymClock(IConfiguration config)
    {
        _zone = TimeZoneInfo.FindSystemTimeZoneById(config["Gym:TimeZone"] ?? "America/New_York");
    }

    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _zone);
    public DateOnly Today => DateOnly.FromDateTime(LocalNow);

    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);
    public DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _zone);
}
