using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Middleware;
using PoliceBackend.Modules.Admin;
using PoliceBackend.Modules.News;
using PoliceBackend.Modules.Police;
using PoliceBackend.Modules.Support;
using PoliceBackend.Modules.User;
using PoliceBackend.Routes;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPoliceBackend(builder.Configuration, builder.Environment);
builder.Services.AddOpenApi();

var app = builder.Build();
var demoOpenAccess = app.Configuration.GetValue("DemoOpenAccess", true);

var authSettings = app.Services.GetRequiredService<AuthSettings>();
if (string.IsNullOrWhiteSpace(authSettings.GoogleClientId))
{
    app.Logger.LogWarning("GOOGLE_CLIENT_ID is not set; Google login will be rejected.");
}

if (!authSettings.HasConfiguredSecret)
{
    app.Logger.LogWarning("AUTH_JWT_SECRET is missing or shorter than 32 characters; using a random key, so restarts sign everyone out.");
}

await app.Services.EnsureDatabaseReadyAsync();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseStaticFiles();
app.UseMiddleware<CorsPreflightMiddleware>();
app.UseCors(CorsPolicyNames.OpenRealtime);
app.UseAuthentication();
app.UseMiddleware<AuthDebugMiddleware>();
app.UseAuthorization();

app.MapSharedRoutes();
app.MapNewsModule();
app.MapUserModule(demoOpenAccess);
app.MapPoliceModule(demoOpenAccess);
app.MapSupportModule(demoOpenAccess);
app.MapAdminModule();
app.MapRealtimeEndpoints(demoOpenAccess);

app.Run();

public partial class Program;
