using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DEFRA.BankHolidays.Health
{
    public sealed class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IDatabaseProbe probe;
        private readonly ILogger<DatabaseHealthCheck> logger;

        public DatabaseHealthCheck(IDatabaseProbe probe, ILogger<DatabaseHealthCheck> logger)
        {
            this.probe = probe;
            this.logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await probe.CheckAsync(cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Database readiness check failed.");
                return HealthCheckResult.Unhealthy("The database is not reachable.");
            }
        }
    }
}
