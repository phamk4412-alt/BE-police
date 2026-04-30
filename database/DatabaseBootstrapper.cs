using Microsoft.EntityFrameworkCore;

namespace PoliceBackend.Database;

public static class DatabaseBootstrapper
{
    public static async Task EnsureDatabaseReadyAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IncidentDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
