using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Middleware;
using PoliceBackend.Modules.Admin;
using PoliceBackend.Modules.Police;
using PoliceBackend.Modules.Support;
using PoliceBackend.Modules.User;
using PoliceBackend.Routes;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPoliceBackend(builder.Configuration, builder.Environment);

var app = builder.Build();
var demoOpenAccess = app.Configuration.GetValue("DemoOpenAccess", true);

await app.Services.EnsureDatabaseReadyAsync();

app.UseMiddleware<CorsPreflightMiddleware>();
app.UseCors(CorsPolicyNames.OpenRealtime);
app.UseAuthentication();
app.UseAuthorization();

app.MapSharedRoutes();
app.MapUserModule(demoOpenAccess);
app.MapPoliceModule(demoOpenAccess);
app.MapSupportModule();
app.MapAdminModule();
app.MapRealtimeEndpoints(demoOpenAccess);

app.Run();

public partial class Program;
