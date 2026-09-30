using Bayubai.Identity;

namespace Bayubai.MigrationService;

internal sealed class MigrationWorker(
    IServiceProvider services,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await services.MigrateIdentityDatabaseAsync(stoppingToken);
            logger.LogInformation("Database migrations applied");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Database migration failed");
            // A non-zero exit code keeps the API from starting in Aspire (WaitForCompletion).
            Environment.ExitCode = 1;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
