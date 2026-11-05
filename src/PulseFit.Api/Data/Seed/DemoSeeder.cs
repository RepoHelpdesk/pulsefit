using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Domain;
using PulseFit.Api.Services;

namespace PulseFit.Api.Data.Seed;

/// <summary>
/// Fills an empty database with three gyms, their staff, about 200 members, two weeks of classes either
/// side of today and the bills, visits and audit trail that go with them. Every date is relative to
/// today, so the data always looks current. The same seed gives the same gym every time.
///
/// A handful of members have a story of their own (see <see cref="Stories"/>): the cases staff ask about.
/// </summary>
public class DemoSeeder
{
    private readonly GymDbContext _db;
    private readonly GymClock _clock;
    private readonly Random _rng = new(42);
    private DateOnly _today;

    private List<Location> _locations = [];
    private Dictionary<PlanTier, Plan> _plans = [];
    private List<Staff> _trainers = [];
    private Staff _owner = null!;
    private List<ClassSession> _sessions = [];

    public DemoSeeder(GymDbContext db, GymClock clock) { _db = db; _clock = clock; }

    /// <summary>Throw everything away and seed again.</summary>
    public async Task ResetAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.Database.EnsureCreatedAsync();
        await SeedAsync();
    }

    /// <summary>Seed only when the database is new.</summary>
    public async Task EnsureSeededAsync()
    {
        await _db.Database.EnsureCreatedAsync();
        if (!await _db.Locations.AnyAsync()) await SeedAsync();
    }

    private async Task SeedAsync()
    {
        _today = _clock.Today;
        SeedGyms();
        await _db.SaveChangesAsync();
        SeedSchedule();
        await _db.SaveChangesAsync();
        await SeedMembersAsync();
        await new Stories(this).AddAsync();
        await _db.SaveChangesAsync();
    }

    // ---- Gyms, plans, staff --------------------------------------------------

    private void SeedGyms()
    {
        _locations =
        [
            new() { Name = "Downtown", Address = "120 Market St", OpensAt = new(5, 0), ClosesAt = new(23, 0) },
            new() { Name = "Riverside", Address = "48 River Rd", OpensAt = new(6, 0), ClosesAt = new(22, 0) },
            new() { Name = "Northgate", Address = "9 Northgate Plaza", OpensAt = new(6, 0), ClosesAt = new(21, 0) },
        ];
        _db.Locations.AddRange(_locations);

        _plans = new()
        {
            [PlanTier.Basic] = new() { Tier = PlanTier.Basic, Name = "Basic", MonthlyPrice = 29m, YearlyPrice = 290m, AllLocations = false, ClassesPerMonth = 0, GuestPassesPerMonth = 0 },
            [PlanTier.Plus] = new() { Tier = PlanTier.Plus, Name = "Plus", MonthlyPrice = 49m, YearlyPrice = 490m, AllLocations = true, ClassesPerMonth = 4, GuestPassesPerMonth = 0 },
            [PlanTier.Premium] = new() { Tier = PlanTier.Premium, Name = "Premium", MonthlyPrice = 79m, YearlyPrice = 790m, AllLocations = true, ClassesPerMonth = null, GuestPassesPerMonth = 2 },
        };
        _db.Plans.AddRange(_plans.Values);

        _db.PromoCodes.AddRange(
            new PromoCode { Code = "NEWYEAR30", PercentOff = 30m, Months = 2, ValidUntil = _today.AddDays(45) },
            new PromoCode { Code = "FRIEND10", PercentOff = 10m, Months = 3, ValidUntil = _today.AddMonths(6) },
            new PromoCode { Code = "SUMMER25", PercentOff = 25m, Months = 1, ValidUntil = _today.AddMonths(-2), Active = false });

        _owner = new Staff { Name = "Rachel Moreno", Email = "rachel@pulsefit.example", Role = StaffRole.Owner };
        _db.Staff.Add(_owner);

        string[] managers = ["Kevin Osei", "Maria Santos", "David Chen"];
        string[] desk = ["Jamie Fox", "Noah Brooks", "Lily Turner"];
        string[][] trainers = [["Chris Adeyemi", "Hannah Weiss"], ["Marco Bianchi", "Sofia Alvarez"], ["Tyler Grant", "Mei Tanaka"]];
        for (var i = 0; i < _locations.Count; i++)
        {
            var loc = _locations[i];
            var mgr = new Staff { Name = managers[i], Email = Email(managers[i], "pulsefit.example"), Role = StaffRole.Manager, Location = loc };
            var fd = new Staff { Name = desk[i], Email = Email(desk[i], "pulsefit.example"), Role = StaffRole.FrontDesk, Location = loc };
            _db.Staff.AddRange(mgr, fd);
            foreach (var t in trainers[i])
            {
                var trainer = new Staff { Name = t, Email = Email(t, "pulsefit.example"), Role = StaffRole.Trainer, Location = loc };
                _db.Staff.Add(trainer);
                _trainers.Add(trainer);
            }
        }
    }

    // ---- Classes -------------------------------------------------------------

    private record Slot(int Hour, int Minute, string Name, int Minutes, int Capacity, string Room);

    private static readonly Slot[] Weekday =
    [
        new(6, 30, "Sunrise Spin", 45, 20, "Spin studio"),
        new(12, 0, "Lunchtime Yoga", 60, 25, "Studio A"),
        new(17, 30, "HIIT Blast", 45, 16, "Studio B"),
        new(19, 0, "Strength Lab", 60, 12, "Weights floor"),
    ];

    private static readonly Slot[] Weekend =
    [
        new(9, 0, "Weekend Yoga", 75, 25, "Studio A"),
        new(10, 30, "HIIT Blast", 45, 16, "Studio B"),
    ];

    private void SeedSchedule()
    {
        for (var day = -14; day <= 7; day++)
        {
            var date = _today.AddDays(day);
            var slots = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? Weekend : Weekday;
            for (var li = 0; li < _locations.Count; li++)
            {
                for (var si = 0; si < slots.Length; si++)
                {
                    var s = slots[si];
                    var trainer = _trainers[li * 2 + (si + day + 100) % 2];
                    var session = new ClassSession
                    {
                        Name = s.Name, Trainer = trainer, Location = _locations[li], Room = s.Room,
                        StartsAt = _clock.ToUtc(date.ToDateTime(new TimeOnly(s.Hour, s.Minute))),
                        DurationMinutes = s.Minutes, Capacity = s.Capacity,
                    };
                    _sessions.Add(session);
                }
            }
        }
        _db.Sessions.AddRange(_sessions);
    }

    // ---- Members -------------------------------------------------------------

    private static readonly string[] FirstNames =
    [
        "Olivia", "Liam", "Emma", "Noah", "Ava", "Ethan", "Mia", "Lucas", "Isabella", "Mason", "Sophia", "Logan",
        "Amelia", "James", "Harper", "Benjamin", "Evelyn", "Elijah", "Abigail", "Oliver", "Emily", "Jacob", "Ella",
        "Michael", "Avery", "Daniel", "Scarlett", "Henry", "Chloe", "Jackson", "Zoe", "Sebastian", "Nora", "Aiden",
        "Riley", "Matthew", "Layla", "Samuel", "Lily", "David", "Aria", "Joseph", "Ellie", "Carter", "Hazel", "Owen",
        "Aurora", "Wyatt", "Violet", "John", "Stella", "Jack", "Lucy", "Luke", "Paisley", "Gabriel", "Naomi", "Isaac",
    ];

    private static readonly string[] LastNames =
    [
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
        "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin",
        "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson", "Walker", "Young",
        "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores", "Green", "Adams", "Nelson",
        "Baker", "Hall", "Rivera", "Campbell", "Mitchell", "Carter", "Roberts", "Okafor", "Novak", "Haddad",
    ];

    private async Task SeedMembersAsync()
    {
        var members = new List<Member>();
        var emails = new HashSet<string>();
        while (members.Count < 196)
        {
            var first = Pick(FirstNames);
            var last = Pick(LastNames);
            var email = Email($"{first} {last}", "mail.example");
            if (!emails.Add(email)) continue;

            var tier = Roll() switch { < 0.35 => PlanTier.Basic, < 0.75 => PlanTier.Plus, _ => PlanTier.Premium };
            var cycle = Roll() < 0.2 ? BillingCycle.Yearly : BillingCycle.Monthly;
            var joined = _today.AddDays(-_rng.Next(20, 760));
            var m = new Member
            {
                FirstName = first, LastName = last, Email = email, Phone = Phone(),
                HomeLocation = Pick(_locations), Plan = _plans[tier], BillingCycle = cycle, JoinedOn = joined,
                Discount = Roll() switch { < 0.08 => DiscountType.Student, < 0.14 => DiscountType.Corporate, _ => DiscountType.None },
                CardStatus = CardStatus.Valid, CardLast4 = _rng.Next(1000, 9999).ToString(), Status = MemberStatus.Active,
            };
            m.NextBillingDate = NextBillingAfter(joined, cycle, _today);
            if (cycle == BillingCycle.Yearly) m.ContractEndsOn = m.NextBillingDate;
            members.Add(m);
        }

        // A realistic mix of states. Each is consistent with the jobs having already run today.
        foreach (var m in members)
        {
            var r = Roll();
            if (r < 0.06)
            {
                // Left: gave notice in the past and it took effect.
                m.CancelRequestedOn = _today.AddDays(-_rng.Next(35, 200));
                m.CancelEffectiveOn = m.CancelRequestedOn.Value.AddDays(GymRules.CancellationNoticeDays);
                m.Status = MemberStatus.Cancelled;
            }
            else if (r < 0.09)
            {
                // Leaving: notice given, not yet taken effect.
                m.CancelRequestedOn = _today.AddDays(-_rng.Next(1, 25));
                m.CancelEffectiveOn = m.CancelRequestedOn.Value.AddDays(GymRules.CancellationNoticeDays);
            }
            else if (r < 0.12)
            {
                m.Status = MemberStatus.Frozen;
                m.FreezeFrom = _today.AddDays(-_rng.Next(1, 25));
                m.FreezeUntil = m.FreezeFrom.Value.AddMonths(1);
                m.FreezeYear = _today.Year;
                m.FreezeMonthsUsed = 1;
            }
            else if (r < 0.15)
            {
                m.CardStatus = Roll() < 0.5 ? CardStatus.Expired : CardStatus.Declined;
                m.PastDueSince = _today.AddDays(-_rng.Next(0, GymRules.PaymentGraceDays));
                m.Status = MemberStatus.PastDue;
            }
            else if (r < 0.17)
            {
                m.CardStatus = CardStatus.Expired;
                m.PastDueSince = _today.AddDays(-_rng.Next(GymRules.PaymentGraceDays, 20));
                m.Status = MemberStatus.Suspended;
            }
        }

        _db.Members.AddRange(members);
        await _db.SaveChangesAsync();

        foreach (var m in members) SeedBillingHistory(m);
        SeedBookings(members);
        SeedVisits(members);
        await _db.SaveChangesAsync();
    }

    /// <summary>Paid bills for the last few cycles; the latest failed for members whose card stopped working.</summary>
    private void SeedBillingHistory(Member m)
    {
        var price = Price(m);
        var cycleStart = m.NextBillingDate;
        for (var i = 0; i < (m.BillingCycle == BillingCycle.Yearly ? 1 : 4); i++)
        {
            cycleStart = m.BillingCycle == BillingCycle.Yearly ? cycleStart.AddYears(-1) : cycleStart.AddMonths(-1);
            if (cycleStart < m.JoinedOn) break;
            if (m.CancelEffectiveOn is { } gone && cycleStart > gone) continue;

            var failed = i == 0 && m.PastDueSince is not null;
            var issued = failed ? m.PastDueSince!.Value : cycleStart;
            var next = MembershipService.NextCycle(cycleStart, m.BillingCycle);
            AddInvoice(m, InvoiceKind.Membership, price, $"{m.Plan!.Name} ({m.BillingCycle.ToString().ToLower()}), {cycleStart:MMM d, yyyy} – {next:MMM d, yyyy}",
                issued, failed ? (m.CardStatus == CardStatus.Expired ? "Card expired" : "Card declined") : null);
        }
        if (m.Status == MemberStatus.Cancelled) m.NextBillingDate = m.CancelEffectiveOn!.Value;
        if (m.Status == MemberStatus.Frozen) AddInvoice(m, InvoiceKind.FreezeFee, GymRules.FreezeFeePerMonth, $"Freeze fee, {m.FreezeFrom:MMM d} – {m.FreezeUntil:MMM d}", m.FreezeFrom!.Value, null);
    }

    /// <summary>Members on plans with classes fill the schedule: past classes attended (a few no-shows), upcoming ones booked.</summary>
    private void SeedBookings(List<Member> members)
    {
        var now = _clock.UtcNow;
        var bookers = members.Where(m => m.Plan!.ClassesPerMonth != 0 && m.Status is MemberStatus.Active or MemberStatus.PastDue).ToList();
        var perMonth = new Dictionary<(int, int), int>();

        foreach (var s in _sessions)
        {
            var target = (int)(s.Capacity * (0.45 + Roll() * 0.6));
            var chosen = bookers.Where(m => m.HomeLocationId == s.Location!.Id || Roll() < 0.1).OrderBy(_ => _rng.Next()).ToList();
            var local = _clock.ToLocal(s.StartsAt);
            var placed = 0;
            foreach (var m in chosen)
            {
                if (placed >= target) break;
                var key = (m.Id, local.Month);
                if (m.Plan!.ClassesPerMonth is { } limit && perMonth.GetValueOrDefault(key) >= limit) continue;
                perMonth[key] = perMonth.GetValueOrDefault(key) + 1;
                var full = placed >= s.Capacity;
                var status = full ? BookingStatus.Waitlisted
                    : s.EndsAt <= now ? (Roll() < 0.06 ? BookingStatus.NoShow : BookingStatus.Attended)
                    : BookingStatus.Booked;
                if (full && s.StartsAt <= now) continue;
                _db.Bookings.Add(new Booking { Session = s, MemberId = m.Id, Status = status, CreatedAt = s.StartsAt.AddDays(-_rng.Next(1, 6)) });
                if (status == BookingStatus.NoShow)
                    AddInvoice(m, InvoiceKind.NoShowFee, GymRules.NoShowFee, $"No-show: {s.Name}, {local:MMM d h:mm tt}", DateOnly.FromDateTime(local), m.CardStatus == CardStatus.Valid ? null : "Card declined");
                placed++;
            }
        }
    }

    /// <summary>Two weeks of visits to the gym.</summary>
    private void SeedVisits(List<Member> members)
    {
        var now = _clock.LocalNow;
        foreach (var m in members.Where(m => m.Status is MemberStatus.Active or MemberStatus.PastDue))
        {
            var visits = _rng.Next(1, 10);
            for (var i = 0; i < visits; i++)
            {
                var day = _today.AddDays(-_rng.Next(0, 14));
                var location = m.Plan!.AllLocations && Roll() < 0.2 ? Pick(_locations) : _locations.First(l => l.Id == m.HomeLocationId);
                var time = day.ToDateTime(new TimeOnly(_rng.Next(location.OpensAt.Hour + 1, location.ClosesAt.Hour - 1), _rng.Next(0, 60)));
                if (time > now) continue;
                _db.CheckIns.Add(new CheckIn { MemberId = m.Id, LocationId = location.Id, At = _clock.ToUtc(time), Allowed = true, StaffId = DeskAt(location).Id });
            }
        }
    }

    // ---- Helpers ------------------------------------------------------------

    internal Invoice AddInvoice(Member m, InvoiceKind kind, decimal amount, string description, DateOnly issued, string? failure, int hour = 9)
    {
        var at = _clock.ToUtc(issued.ToDateTime(new TimeOnly(hour, 0)));
        var invoice = new Invoice
        {
            Member = m, MemberId = m.Id, Kind = kind, Amount = amount, Description = description, IssuedAt = at,
            Status = failure is null ? InvoiceStatus.Paid : InvoiceStatus.Failed,
            PaidAt = failure is null ? at : null, FailedAt = failure is null ? null : at, FailureReason = failure,
        };
        _db.Invoices.Add(invoice);
        return invoice;
    }

    internal decimal Price(Member m)
    {
        var price = m.Plan!.PriceFor(m.BillingCycle);
        var off = m.Discount switch { DiscountType.Student => GymRules.StudentDiscountPercent, DiscountType.Corporate => GymRules.CorporateDiscountPercent, _ => 0m };
        return Math.Round(price * (100m - off) / 100m, 2, MidpointRounding.AwayFromZero);
    }

    internal static DateOnly NextBillingAfter(DateOnly joined, BillingCycle cycle, DateOnly today)
    {
        var d = joined;
        while (d <= today) d = MembershipService.NextCycle(d, cycle);
        return d;
    }

    private double Roll() => _rng.NextDouble();
    private T Pick<T>(IReadOnlyList<T> items) => items[_rng.Next(items.Count)];
    private string Phone() => $"(555) {_rng.Next(200, 999)}-{_rng.Next(1000, 9999)}";
    internal static string Email(string name, string domain) => $"{name.ToLower().Replace(' ', '.')}@{domain}";

    // What the stories need from the seeded gym.
    internal GymDbContext Db => _db;
    internal GymClock Clock => _clock;
    internal DateOnly Today => _today;
    internal IReadOnlyList<Location> Locations => _locations;
    internal IReadOnlyDictionary<PlanTier, Plan> Plans => _plans;
    internal IReadOnlyList<ClassSession> Sessions => _sessions;
    internal Staff Owner => _owner;
    internal Staff ManagerAt(Location l) => _db.Staff.Local.First(s => s.Role == StaffRole.Manager && s.LocationId == l.Id);
    internal Staff DeskAt(Location l) => _db.Staff.Local.First(s => s.Role == StaffRole.FrontDesk && s.LocationId == l.Id);
}
