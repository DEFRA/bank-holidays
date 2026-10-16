using DEFRA.BankHolidays.DAL;
using DEFRA.BankHolidays.Migrations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Localization;
using Newtonsoft.Json.Serialization;
using System.Data.Entity.Migrations;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("HolidayContext")
    ?? throw new InvalidOperationException("Connection string 'HolidayContext' is not configured.");

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

builder.Services.AddHttpClient();
builder.Services.AddScoped(_ => new HolidayContext(connectionString));

if (!string.IsNullOrEmpty(builder.Configuration["ApplicationInsights:ConnectionString"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Bank Holiday Service", Version = "v1" });
});

var app = builder.Build();

// Schema changes are normally applied by the deployment pipeline; this switch keeps the previous
// "migrate on start-up" behaviour available for environments that still need it.
if (builder.Configuration.GetValue<bool>("ApplyMigrationsOnStartup"))
{
    new DbMigrator(new Configuration(connectionString)).Update();
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

app.MapControllers();
app.MapControllerRoute(
    name: "Default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
