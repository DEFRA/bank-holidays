using System.Net;
using DEFRA.BankHolidays.Tests.Controllers;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DEFRA.BankHolidays.Tests.Integration
{
    [TestClass]
    public class ApiIntegrationTest
    {
        [TestMethod]
        public async Task Health_live_is_ok_even_when_the_database_is_down()
        {
            using var factory = new AppFactory();
            Mock.Get(factory.Probe).Setup(x => x.CheckAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/health/live");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        public async Task Health_ready_is_ok_when_the_database_is_reachable()
        {
            using var factory = new AppFactory();
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/health/ready");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(nameof(HealthStatus.Healthy), await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Health_ready_is_unavailable_when_the_database_is_down()
        {
            using var factory = new AppFactory();
            Mock.Get(factory.Probe).Setup(x => x.CheckAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("server=secret-host"));
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/health/ready");
            var body = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.IsFalse(body.Contains("secret-host"));
        }

        [TestMethod]
        public async Task Anonymous_callers_can_read_all_holidays_with_the_declared_json_names()
        {
            using var factory = new AppFactory();
            AppFactory.SeedMay2018Holiday(factory.Data);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/values");
            var body = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            StringAssert.Contains(response.Content.Headers.ContentType.ToString(), "application/json");
            StringAssert.Contains(body, "\"england-and-wales\"");
            StringAssert.Contains(body, "\"Division\"");
            StringAssert.Contains(body, "\"Events\"");
        }

        [TestMethod]
        public async Task Reading_holidays_returns_no_content_when_there_is_no_data()
        {
            using var factory = new AppFactory();
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/values");

            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        [TestMethod]
        [DataRow("2018-05-28", "true")]
        [DataRow("2018-05-27", "false")]
        public async Task Anonymous_callers_can_check_a_date(string date, string expected)
        {
            using var factory = new AppFactory();
            AppFactory.SeedMay2018Holiday(factory.Data);
            using var client = factory.CreateClient();

            var response = await client.GetAsync($"/api/values?date={date}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(expected, await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task The_legacy_workingdays_route_used_by_existing_consumers_still_works()
        {
            using var factory = new AppFactory();
            AppFactory.SeedMay2018Holiday(factory.Data);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/Values/workingdays?start=2018-05-25&end=2018-05-29");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("1", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Swagger_document_and_home_page_are_available_anonymously()
        {
            using var factory = new AppFactory();
            using var client = factory.CreateClient();

            var swagger = await client.GetAsync("/swagger/v1/swagger.json");
            var home = await client.GetAsync("/");

            Assert.AreEqual(HttpStatusCode.OK, swagger.StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, home.StatusCode);
            Assert.IsFalse((await home.Content.ReadAsStringAsync()).Contains("gov.uk/bank-holidays.json"));
        }
    }
}
