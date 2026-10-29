using System.Net;
using DEFRA.BankHolidays.Settings;
using DEFRA.BankHolidays.Tests.Controllers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DEFRA.BankHolidays.Tests.Integration
{
    [TestClass]
    public class WriteEndpointAuthorizationTest
    {
        private static HttpRequestMessage Post(string url, string roles = null, HttpContent content = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

            if (roles != null)
            {
                request.Headers.Add(TestAuthHandler.RolesHeader, roles);
            }

            return request;
        }

        private static FormUrlEncodedContent HolidayForm()
        {
            return new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("data", HomeControllerTest.SampleJson) });
        }

        [TestMethod]
        [DataRow("/Home/Update")]
        [DataRow("/Home/Refresh")]
        public async Task Write_endpoints_reject_callers_without_a_token(string url)
        {
            using var factory = new AppFactory(entraConfigured: true);
            using var client = factory.CreateClient();

            var response = await client.SendAsync(Post(url, content: HolidayForm()));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(0, factory.Data.Count);
        }

        [TestMethod]
        [DataRow("/Home/Update")]
        [DataRow("/Home/Refresh")]
        public async Task Write_endpoints_reject_callers_without_the_refresh_role(string url)
        {
            using var factory = new AppFactory(entraConfigured: true);
            using var client = factory.CreateClient();

            var response = await client.SendAsync(Post(url, roles: "Some.Other.Role", content: HolidayForm()));

            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.AreEqual(0, factory.Data.Count);
        }

        [TestMethod]
        public async Task Update_with_the_refresh_role_replaces_the_data()
        {
            using var factory = new AppFactory(entraConfigured: true);
            using var client = factory.CreateClient();

            var response = await client.SendAsync(Post("/Home/Update", AuthorizationPolicies.RefreshRole));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, factory.Data.Count);
            Assert.AreEqual("england-and-wales", factory.Data[0].EnglandWales.Division);
        }

        [TestMethod]
        public async Task Refresh_with_the_refresh_role_replaces_the_data()
        {
            using var factory = new AppFactory(entraConfigured: true);
            using var client = factory.CreateClient();

            var response = await client.SendAsync(Post("/Home/Refresh", AuthorizationPolicies.RefreshRole, HolidayForm()));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("Ok", await response.Content.ReadAsStringAsync());
            Assert.AreEqual(1, factory.Data.Count);
        }

        [TestMethod]
        public async Task Update_reports_a_bad_gateway_when_the_source_fails_and_does_not_leak_details()
        {
            using var factory = new AppFactory(entraConfigured: true);
            factory.Upstream = () => throw new HttpRequestException("connection refused by internal-host");
            using var client = factory.CreateClient();

            var response = await client.SendAsync(Post("/Home/Update", AuthorizationPolicies.RefreshRole));
            var body = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
            Assert.IsFalse(body.Contains("internal-host"));
        }

        [TestMethod]
        public async Task Read_endpoints_stay_anonymous_when_entra_is_configured()
        {
            using var factory = new AppFactory(entraConfigured: true);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/values?date=2018-05-28");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        public async Task Anonymous_writes_are_allowed_only_in_development_when_explicitly_enabled()
        {
            using var factory = new AppFactory(environment: "Development", entraConfigured: false, allowAnonymousWrites: true);
            using var client = factory.CreateClient();

            var response = await client.SendAsync(Post("/Home/Update"));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
