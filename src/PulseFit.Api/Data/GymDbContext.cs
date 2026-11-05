using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Domain;

namespace PulseFit.Api.Data;

public class GymDbContext : DbContext
{
    public GymDbContext(DbContextOptions<GymDbContext> options) : base(options) { }

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<ClassSession> Sessions => Set<ClassSession>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<GuestPass> GuestPasses => Set<GuestPass>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    public DbSet<JobRun> JobRuns => Set<JobRun>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // SQLite has no decimal type, and EF won't sum or sort one there. Cents are fine as a double.
        builder.Properties<decimal>().HaveConversion<double>();
        builder.Properties<Enum>().HaveConversion<string>();
        // Every time is UTC; SQLite forgets that, so say it again on the way out.
        builder.Properties<DateTime>().HaveConversion<UtcConverter>();
        builder.Properties<DateTime?>().HaveConversion<UtcConverter>();
    }

    private class UtcConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>
    {
        public UtcConverter() : base(v => DateTime.SpecifyKind(v, DateTimeKind.Utc), v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Member>().HasIndex(m => m.Email).IsUnique();
        b.Entity<Member>().HasOne(m => m.Plan).WithMany().HasForeignKey(m => m.PlanId);
        b.Entity<Member>().HasOne<Plan>().WithMany().HasForeignKey(m => m.PendingPlanId);
        b.Entity<PromoCode>().HasIndex(p => p.Code).IsUnique();
        b.Entity<Booking>().HasIndex(x => new { x.SessionId, x.MemberId });
        b.Entity<Booking>().HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId);
        b.Entity<Invoice>().HasIndex(i => new { i.MemberId, i.IssuedAt });
        b.Entity<AuditEntry>().HasIndex(a => new { a.MemberId, a.At });
        b.Entity<Refund>().HasOne(r => r.RequestedBy).WithMany().HasForeignKey(r => r.RequestedByStaffId);
    }
}
