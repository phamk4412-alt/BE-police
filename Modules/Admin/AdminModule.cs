using PoliceBackend.Config;
using PoliceBackend.Controllers;

namespace PoliceBackend.Modules.Admin;

public static class AdminModule
{
    public static IEndpointRouteBuilder MapAdminModule(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/audit-logs", AdminController.GetAuditLogsAsync)
            .RequireAuthorization(AuthorizationPolicies.CanAuditAndExport);

        api.MapGet("/incidents/export", AdminController.ExportIncidentsAsync)
            .RequireAuthorization(AuthorizationPolicies.CanAuditAndExport);

        api.MapGet("/admin/statistics", AdminController.GetStatisticsAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        api.MapGet("/admin/clerk/accounts", AdminController.GetClerkAccountsAsync);

        api.MapPatch("/admin/clerk/accounts/{userId}/role", AdminController.UpdateClerkAccountRoleAsync);

        api.MapPatch("/admin/clerk/accounts/{userId}/status", AdminController.UpdateClerkAccountStatusAsync);

        api.MapDelete("/admin/clerk/accounts/{userId}", AdminController.DeleteClerkAccountAsync);

        api.MapGet("/admin/system/health", AdminController.GetSystemHealth)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}
