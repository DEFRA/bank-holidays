using System.Net;
using System.Text.RegularExpressions;
using DEFRA.BankHolidays.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DEFRA.BankHolidays.Tests.Integration
{
    [TestClass]
    public class ManualRefreshTest
    {
        private static async Task<FormUrlEncodedContent> RefreshForm(HttpClient client)
        {
            var response = await client.GetAsync("/");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            StringAssert.Contains(html, "id=\"refresh-button\"");
            StringAssert.Contains(html, "action=\"/Home/ManualRefresh\"");
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.IsTrue(token.Success);

            return new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", WebUtility.HtmlDecode(token.Groups[1].Value))
            });
        }

        [TestMethod]
        public async Task Anonymous_user_can_refresh_and_read_back_source_data_with_entra_configured()
        {
            using var factory = new AppFactory(environment: "Production", entraConfigured: true);
            factory.SeedMay2018Holiday();
            using var client = factory.CreateClient();
            using var form = await RefreshForm(client);

            var response = await client.PostAsync("/Home/ManualRefresh", form);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, factory.Data.Count);

            var result = await client.GetAsync("/api/values?date=2018-12-25");
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            Assert.AreEqual("true", await result.Content.ReadAsStringAsync());
            var oldDate = await client.GetAsync("/api/values?date=2018-05-28");
            Assert.AreEqual("false", await oldDate.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Manual_refresh_rejects_missing_or_invalid_antiforgery_tokens()
        {
            using var factory = new AppFactory(entraConfigured: true);
            using var client = factory.CreateClient();
            using var validForm = await RefreshForm(client);

            var missing = await client.PostAsync("/Home/ManualRefresh", new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));
            var invalid = await client.PostAsync("/Home/ManualRefresh", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", "invalid")
            }));

            Assert.AreEqual(HttpStatusCode.BadRequest, missing.StatusCode);
            Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.AreEqual(0, factory.Data.Count);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Source_failure_preserves_existing_data_and_returns_generic_error(bool invalidJson)
        {
            using var factory = new AppFactory(entraConfigured: true);
            factory.SeedMay2018Holiday();
            factory.Upstream = invalidJson
                ? () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid JSON") }
                : () => throw new HttpRequestException("secret-upstream");
            using var client = factory.CreateClient();
            using var form = await RefreshForm(client);

            var response = await client.PostAsync("/Home/ManualRefresh", form);

            Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
            Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("secret-upstream"));
            var result = await client.GetAsync("/api/values?date=2018-05-28");
            Assert.AreEqual("true", await result.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Manual_refresh_uses_the_shared_guard_and_requires_post()
        {
            using var factory = new AppFactory(entraConfigured: true);
            using var client = factory.CreateClient();
            using var form = await RefreshForm(client);
            using var lease = factory.Services.GetRequiredService<RefreshGate>().TryAcquire();
            Assert.IsNotNull(lease);

            var response = await client.PostAsync("/Home/ManualRefresh", form);
            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
            Assert.AreEqual(0, factory.Data.Count);
            var get = await client.GetAsync("/Home/ManualRefresh");
            Assert.AreEqual(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        }
    }
}
