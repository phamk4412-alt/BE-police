namespace PoliceBackend.Config;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
    public const string Police = "Police";
    public const string Support = "Support";
}

public static class AuthorizationPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string UserOnly = nameof(UserOnly);
    public const string PoliceOnly = nameof(PoliceOnly);
    public const string SupportOnly = nameof(SupportOnly);
    public const string CanSubmitIncident = nameof(CanSubmitIncident);
    public const string CanViewIncidents = nameof(CanViewIncidents);
    public const string CanTrackIncident = nameof(CanTrackIncident);
    public const string CanUpdateIncidents = nameof(CanUpdateIncidents);
    public const string CanAuditAndExport = nameof(CanAuditAndExport);
}

public static class DatabaseProviders
{
    public const string InMemory = "inmemory";
    public const string SqlServer = "sqlserver";
    public const string Postgres = "postgres";
}

public static class CorsPolicyNames
{
    public const string OpenRealtime = nameof(OpenRealtime);
}

public static class IncidentStatuses
{
    public const string MoiTiepNhan = "Moi tiep nhan";
    public const string DaTiepNhan = "Da tiep nhan";
    public const string DangXacMinh = "Dang xac minh";
    public const string DaDieuPhoi = "Da dieu phoi";
    public const string DaXuLy = "Da xu ly";
}

public static class AuditActions
{
    public const string Register = "auth.register";
    public const string LoginSuccess = "auth.login.success";
    public const string LoginFailed = "auth.login.failed";
    public const string Logout = "auth.logout";
    public const string AnalyzeIncident = "incident.analyze";
    public const string CreateIncident = "incident.create";
    public const string UpdateIncidentStatus = "incident.status.update";
    public const string UpdateIncidentDenied = "incident.status.denied";
    public const string ExportIncidents = "incident.export";
}

public static class AuditEntities
{
    public const string Auth = "auth";
    public const string Incident = "incident";
    public const string Dispatch = "dispatch";
    public const string Admin = "admin";
    public const string Map = "map";
}
