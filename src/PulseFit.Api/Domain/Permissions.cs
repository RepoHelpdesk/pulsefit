namespace PulseFit.Api.Domain;

/// <summary>
/// What each staff role may do. The front desk runs the door and the day-to-day; trainers run their
/// classes; managers handle money and memberships; the owner sets prices and approves big refunds.
/// </summary>
public enum Permission
{
    ViewMembers,
    EditMembers,
    CheckIn,
    IssueGuestPass,
    BookClasses,
    MarkAttendance,
    ManageClasses,
    FreezeMembership,
    CancelMembership,
    ChangePlan,
    WaiveFee,
    RequestRefund,
    ApproveRefund,
    ManagePlans,
    ViewReports,
    RunJobs,
    ViewAudit,
}

public static class Permissions
{
    private static readonly Dictionary<StaffRole, HashSet<Permission>> Grants = new()
    {
        [StaffRole.FrontDesk] = [Permission.ViewMembers, Permission.EditMembers, Permission.CheckIn,
            Permission.IssueGuestPass, Permission.BookClasses, Permission.WaiveFee],
        [StaffRole.Trainer] = [Permission.ViewMembers, Permission.MarkAttendance, Permission.BookClasses],
        [StaffRole.Manager] = [Permission.ViewMembers, Permission.EditMembers, Permission.CheckIn,
            Permission.IssueGuestPass, Permission.BookClasses, Permission.MarkAttendance, Permission.ManageClasses,
            Permission.FreezeMembership, Permission.CancelMembership, Permission.ChangePlan, Permission.WaiveFee,
            Permission.RequestRefund, Permission.ViewReports, Permission.ViewAudit],
        [StaffRole.Owner] = Enum.GetValues<Permission>().ToHashSet(),
    };

    public static bool Can(this Staff staff, Permission p) => Grants[staff.Role].Contains(p);

    /// <summary>Stop here, with a message the staff member sees, unless their role may do this.</summary>
    public static void Require(this Staff staff, Permission p)
    {
        if (!staff.Can(p)) throw new Services.GymRuleException("forbidden", $"Your role ({staff.Role}) can't do that.");
    }

    public static IReadOnlyCollection<Permission> For(StaffRole role) => Grants[role];
}
