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

        api.MapGet("/admin/accounts", AdminController.GetAccountsAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        api.MapPatch("/admin/accounts/{userId:guid}/role", AdminController.UpdateAccountRoleAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        api.MapPatch("/admin/accounts/{userId:guid}/status", AdminController.UpdateAccountStatusAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        api.MapDelete("/admin/accounts/{userId:guid}", AdminController.DeleteAccountAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        api.MapGet("/admin/system/health", AdminController.GetSystemHealth)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}
