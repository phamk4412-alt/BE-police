using PoliceBackend.Config;

namespace PoliceBackend.Middleware;

public sealed class CorsPreflightMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (AllowedOriginPolicy.IsAllowedOrigin(origin))
        {
            context.Response.Headers.AccessControlAllowOrigin = origin;
            context.Response.Headers.AccessControlAllowCredentials = "true";
            context.Response.Headers.AccessControlAllowHeaders = "Content-Type, Authorization, X-Requested-With, X-SignalR-User-Agent";
            context.Response.Headers.AccessControlAllowMethods = "GET, POST, PATCH, PUT, DELETE, OPTIONS";
            context.Response.Headers.Vary = "Origin";
        }

        if (HttpMethods.IsOptions(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await next(context);
    }
}
