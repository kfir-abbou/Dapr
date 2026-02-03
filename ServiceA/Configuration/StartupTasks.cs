using ServiceA.Services;

namespace ServiceA.Configuration;

public static class StartupTasks
{
    public static void RegisterStartupTasks(this WebApplication app)
    {
        app.Lifetime.ApplicationStarted.Register(async () =>
        {
            // Wait for Dapr sidecar to be ready
            await Task.Delay(2000);

            using var scope = app.Services.CreateScope();
            var stateBackupService = scope.ServiceProvider.GetRequiredService<StateBackupService>();
            await stateBackupService.InitializeStateAsync();
        });
    }
}
