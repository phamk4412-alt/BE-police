using System.Security.Claims;

namespace PoliceBackend.Middleware;

public sealed class AuthDebugMiddleware(RequestDelegate next, ILogger<AuthDebugMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals("/api/incidents", StringComparison.OrdinalIgnoreCase) &&
            HttpMethods.IsPost(context.Request.Method))
        {
            var user = context.User;
            logger.LogInformation(
                "POST /api/incidents auth state before authorization: isAuthenticated={IsAuthenticated}, authType={AuthType}, user={User}, role={Role}, hasAuthorizationHeader={HasAuthorizationHeader}, hasBackendCookie={HasBackendCookie}.",
                user.Identity?.IsAuthenticated == true,
                user.Identity?.AuthenticationType ?? "(none)",
                user.Identity?.Name ?? "(none)",
                user.FindFirstValue(ClaimTypes.Role) ?? "(none)",
                context.Request.Headers.ContainsKey("Authorization"),
                context.Request.Cookies.ContainsKey("PoliceSmartHub.Auth"));
        }

        await next(context);
    }
}
