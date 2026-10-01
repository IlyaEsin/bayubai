using Bayubai.Identity;
using Bayubai.SharedKernel.Persistence;
using Npgsql;

namespace Bayubai.MigrationService;

internal sealed class MigrationWorker(
    IServiceProvider services,
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await services.MigrateIdentityDatabaseAsync(stoppingToken);
            logger.LogInformation("Database migrations applied");

            // Grants run after every migration so tables added by it are readable by the API in the same deploy.
            var role = configuration.GetValue<string>("Database:AppRole")
                ?? throw new InvalidOperationException("Database:AppRole is not configured.");
            var password = configuration.GetValue<string>("Database:AppRolePassword")
                ?? throw new InvalidOperationException("Database:AppRolePassword is not configured.");
            await using (var connection = new NpgsqlConnection(configuration.GetConnectionString(IdentityModule.ConnectionStringName)))
            {
                await connection.OpenAsync(stoppingToken);
                await PostgresAccess.EnsureLoginRoleAsync(connection, role, password, stoppingToken);
            }

            await services.GrantIdentityDatabaseAccessAsync(role, stoppingToken);
            logger.LogInformation("Database access granted to the application role");
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
