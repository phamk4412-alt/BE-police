using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using PoliceBackend.Config;

namespace PoliceBackend.Utils;

public static class AuthRedirectUtils
{
    public static Task HandleRedirectAsync(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        if (IsApiOrHubRequest(context.Request))
        {
            context.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        }

        context.Response.Redirect("/");
        return Task.CompletedTask;
    }

    public static bool IsApiOrHubRequest(HttpRequest request)
    {
        return request.Path.StartsWithSegments("/api") || request.Path.StartsWithSegments("/hubs");
    }

    public static string GetLandingPathForRole(string? role)
    {
        return role switch
        {
            AppRoles.Admin => "/admin/admin.html",
            AppRoles.User => "/user/user.html",
            AppRoles.Police => "/police/police.html",
            AppRoles.Support => "/support/support.html",
            _ => "/"
        };
    }
}
