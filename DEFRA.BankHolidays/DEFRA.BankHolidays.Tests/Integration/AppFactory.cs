using DEFRA.BankHolidays.Controllers;
using DEFRA.BankHolidays.DAL;
using DEFRA.BankHolidays.Health;
using DEFRA.BankHolidays.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace DEFRA.BankHolidays.Tests.Integration
{
    // Runs the real application pipeline (routing, formatters, authorization, health checks) with the
    // database, upstream feed and token validation replaced by test doubles.
    internal sealed class AppFactory : WebApplicationFactory<Program>
    {
        private readonly string environment;
        private readonly Dictionary<string, string> originalEnvironmentVariables = new();

        public AppFactory(string environment = "Development", bool entraConfigured = false, bool? allowAnonymousWrites = null)
        {
            this.environment = environment;

            // Program reads configuration before the host is built, so settings are supplied as environment
            // variables (which also take precedence over appsettings*.json).
            Set("ConnectionStrings__HolidayContext", "Data Source=unused; Initial Catalog=unused; Encrypt=False");
            Set("AzureAd__TenantId", entraConfigured ? "00000000-0000-0000-0000-000000000001" : "");
            Set("AzureAd__ClientId", entraConfigured ? "00000000-0000-0000-0000-000000000002" : "");
            Set("Authentication__AllowAnonymousWritesInDevelopment", allowAnonymousWrites?.ToString() ?? "");

            Data = new List<Holidays>();
            Upstream = () => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(Controllers.HomeControllerTest.SampleJson) };
            Probe = Mock.Of<IDatabaseProbe>();
        }

        public List<Holidays> Data { get; }

        public Func<HttpResponseMessage> Upstream { get; set; }

        public IDatabaseProbe Probe { get; set; }

        private void Set(string name, string value)
        {
            originalEnvironmentVariables[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, string.IsNullOrEmpty(value) ? null : value);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<HolidayContext>();
                services.AddScoped(_ => MockDb.CreateContext(MockDb.CreateHolidaysSet(Data), Data).Object);

                services.RemoveAll<IDatabaseProbe>();
                services.AddSingleton(Probe);

                services.AddHttpClient(HomeController.SourceClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(Upstream));

                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                        options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            foreach (var item in originalEnvironmentVariables)
            {
                Environment.SetEnvironmentVariable(item.Key, item.Value);
            }
        }

        public static void SeedMay2018Holiday(List<Holidays> data)
        {
            data.Add(new Holidays
            {
                EnglandWales = new EnglandWales
                {
                    Division = "england-and-wales",
                    Events = new List<Event> { new Event { Title = "Spring bank holiday", Date = new DateTime(2018, 5, 28) } }
                }
            });
        }
    }
}
