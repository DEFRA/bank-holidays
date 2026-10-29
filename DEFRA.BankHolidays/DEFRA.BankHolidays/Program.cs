using DEFRA.BankHolidays.Controllers;
using DEFRA.BankHolidays.DAL;
using DEFRA.BankHolidays.Health;
using DEFRA.BankHolidays.Migrations;
using DEFRA.BankHolidays.Services;
using DEFRA.BankHolidays.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Newtonsoft.Json.Serialization;

MigrationCommand command;

try
{
    command = MigrationCommands.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

// Command-line utilities (--migrate, --script) run and exit without starting the web host.
var builder = WebApplication.CreateBuilder(command == null ? args : Array.Empty<string>());

builder.Configuration.AddOptionalKeyVault();

var connectionString = builder.Configuration.GetConnectionString("HolidayContext")
    ?? throw new InvalidOperationException("Connection string 'HolidayContext' is not configured.");

if (command != null)
{
    try
    {
        return MigrationCommands.Run(command, connectionString);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Migration command failed: {ex.Message}");
        return 1;
    }
}

builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson(options =>
    {
        // Web API 2 serialised property names as declared (no camelCase); keep the JSON contract identical.
        options.SerializerSettings.ContractResolver = new DefaultContractResolver();
    });

// Web API 2 also returned JSON to clients that ask for text/html (e.g. browsers).
builder.Services.Configure<MvcOptions>(options =>
{
    options.OutputFormatters.OfType<NewtonsoftJsonOutputFormatter>().First()
        .SupportedMediaTypes.Add("text/html");
});

builder.Services.AddOptions<HolidaySourceOptions>()
    .Bind(builder.Configuration.GetSection(HolidaySourceOptions.SectionName))
    .Validate(HolidaySourceOptions.IsValid, "BankHolidays:SourceUrl must be an absolute http(s) URL and BankHolidays:SourceTimeoutSeconds must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddHttpClient(HomeController.SourceClientName, (services, client) =>
{
    client.Timeout = TimeSpan.FromSeconds(services.GetRequiredService<IOptions<HolidaySourceOptions>>().Value.SourceTimeoutSeconds);
});

builder.Services.AddSingleton<RefreshGate>();
builder.Services.AddScoped(_ => new HolidayContext(connectionString));

var telemetryConnectionString = TelemetrySettings.ResolveConnectionString(builder.Configuration);

if (telemetryConnectionString != null)
{
    builder.Services.AddApplicationInsightsTelemetry(options => options.ConnectionString = telemetryConnectionString);
}

builder.Services.AddSingleton<IDatabaseProbe>(_ => new SqlDatabaseProbe(connectionString));
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

// Endpoints that change data require an Entra ID access token carrying the BankHolidays.Refresh app role.
// Reads stay anonymous. Without Entra configuration the app only starts in Development, and only when
// anonymous writes are explicitly allowed, so a deployment cannot accidentally expose the write endpoints.
var azureAd = builder.Configuration.GetSection("AzureAd");
var entraConfigured = !string.IsNullOrWhiteSpace(azureAd["TenantId"]) && !string.IsNullOrWhiteSpace(azureAd["ClientId"]);
var allowAnonymousWrites = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Authentication:AllowAnonymousWritesInDevelopment");

if (entraConfigured)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(azureAd);
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(AuthorizationPolicies.Refresh, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(AuthorizationPolicies.RefreshRole));
}
else if (allowAnonymousWrites)
{
    builder.Services.AddAuthentication();
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(AuthorizationPolicies.Refresh, policy => policy.RequireAssertion(_ => true));
}
else
{
    throw new InvalidOperationException(
        "Microsoft Entra ID is not configured. Set 'AzureAd:TenantId' and 'AzureAd:ClientId'. " +
        "Anonymous writes can only be enabled in Development with 'Authentication:AllowAnonymousWritesInDevelopment'.");
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Bank Holiday Service", Version = "v1" });
});

var app = builder.Build();

// Schema changes are normally applied by the deployment pipeline (--migrate or the generated SQL script);
// this switch keeps the previous "migrate on start-up" behaviour available for local development.
if (builder.Configuration.GetValue<bool>("ApplyMigrationsOnStartup"))
{
    if (!app.Environment.IsDevelopment())
    {
        app.Logger.LogWarning("ApplyMigrationsOnStartup is enabled outside Development. Several instances starting together can race; prefer the --migrate command.");
    }

    MigrationCommands.Run(new MigrationCommand(MigrationAction.Migrate, null), connectionString);
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en-GB")
    .AddSupportedCultures("en-GB")
    .AddSupportedUICultures("en-GB"));

app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("v1/swagger.json", "Bank Holiday Service v1");
    options.RoutePrefix = "swagger";
});

// The old API help page lived at /Help
app.MapGet("/Help", (HttpContext context) => Results.Redirect(context.Request.PathBase + "/swagger"));

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Liveness reports that the process is running; readiness also checks that the database can be reached.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapControllers();
app.MapControllerRoute(
    name: "Default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

return 0;

public partial class Program { }
