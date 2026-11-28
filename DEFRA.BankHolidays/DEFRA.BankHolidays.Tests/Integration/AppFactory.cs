using DEFRA.BankHolidays.Controllers;
using DEFRA.BankHolidays.DAL;
using DEFRA.BankHolidays.Health;
using DEFRA.BankHolidays.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace DEFRA.BankHolidays.Tests.Integration
{
    // Runs the real application pipeline (routing, formatters, authorization, health checks) with the
    // database (in-memory SQLite), upstream feed and token validation replaced by test doubles.
    internal sealed class AppFactory : WebApplicationFactory<Program>
    {
        private readonly string environment;
        private readonly Dictionary<string, string> originalEnvironmentVariables = new();
        private readonly TestDb db = new();

        public AppFactory(string environment = "Development", bool entraConfigured = false, bool? allowAnonymousWrites = null)
        {
            this.environment = environment;

            // Program reads configuration before the host is built, so settings are supplied as environment
            // variables (which also take precedence over appsettings*.json).
            Set("ConnectionStrings__HolidayContext", "Data Source=unused; Initial Catalog=unused; Encrypt=False");
            Set("AzureAd__TenantId", entraConfigured ? "00000000-0000-0000-0000-000000000001" : "");
            Set("AzureAd__ClientId", entraConfigured ? "00000000-0000-0000-0000-000000000002" : "");
            Set("Authentication__AllowAnonymousWritesInDevelopment", allowAnonymousWrites?.ToString() ?? "");

            Upstream = () => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(Controllers.HomeControllerTest.SampleJson) };
            Probe = Mock.Of<IDatabaseProbe>();
        }

        // What is currently persisted, read fresh on every access.
        public List<Holidays> Data => db.Data;

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
                // EF Core 9+ keeps the provider setup in this registration; leaving it would register SQL Server alongside SQLite.
                services.RemoveAll<IDbContextOptionsConfiguration<HolidayContext>>();
                services.RemoveAll<DbContextOptions<HolidayContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<HolidayContext>();
                services.AddDbContext<HolidayContext>(options => options.UseSqlite(db.Connection));

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

            if (disposing)
            {
                db.Dispose();
            }

            foreach (var item in originalEnvironmentVariables)
            {
                Environment.SetEnvironmentVariable(item.Key, item.Value);
            }
        }

        public void SeedMay2018Holiday()
        {
            db.Add(new Holidays
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
