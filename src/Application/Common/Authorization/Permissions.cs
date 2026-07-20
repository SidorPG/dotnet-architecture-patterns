namespace Application.Common.Authorization;

/// <summary>
/// Centralised permission string constants.
/// Commands declare which permission they need directly on the record:
///   public string? RequiredPermission => Permissions.JoinRequests.InstructorWrite;
/// </summary>
public static class Permissions
{
    public static class Students    { public const string Read = "students:read";    public const string Write = "students:write"; }
    public static class Groups      { public const string Read = "groups:read";      public const string Write = "groups:write"; }
    public static class Schedules   { public const string Read = "schedules:read";   public const string Write = "schedules:write"; }
    public static class Vehicles    { public const string Read = "vehicles:read";    public const string Write = "vehicles:write"; }
    public static class Locations   { public const string Read = "locations:read";   public const string Write = "locations:write"; }

    public static class JoinRequests
    {
        public const string Read            = "joinrequests:read";
        public const string StudentWrite    = "joinrequests:student";
        public const string InstructorWrite = "joinrequests:instructor";
    }

    public static class Dashboard
    {
        public const string StudentRead    = "dashboard:read:student";
        public const string InstructorRead = "dashboard:read:instructor";
        public const string AdminRead      = "dashboard:read:admin";
    }
}
