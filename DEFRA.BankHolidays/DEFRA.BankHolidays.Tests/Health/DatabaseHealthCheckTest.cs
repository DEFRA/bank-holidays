using DEFRA.BankHolidays.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DEFRA.BankHolidays.Tests.Health
{
    [TestClass]
    public class DatabaseHealthCheckTest
    {
        [TestMethod]
        public async Task Reports_healthy_when_the_database_is_reachable()
        {
            var probe = new Mock<IDatabaseProbe>();
            probe.Setup(x => x.CheckAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var check = new DatabaseHealthCheck(probe.Object, NullLogger<DatabaseHealthCheck>.Instance);

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.AreEqual(HealthStatus.Healthy, result.Status);
        }

        [TestMethod]
        public async Task Reports_unhealthy_without_leaking_the_failure_when_the_database_is_not_reachable()
        {
            var probe = new Mock<IDatabaseProbe>();
            probe.Setup(x => x.CheckAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("server=secret-host"));
            var check = new DatabaseHealthCheck(probe.Object, NullLogger<DatabaseHealthCheck>.Instance);

            var result = await check.CheckHealthAsync(new HealthCheckContext());

            Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
            Assert.IsFalse(result.Description.Contains("secret-host"));
        }
    }
}
