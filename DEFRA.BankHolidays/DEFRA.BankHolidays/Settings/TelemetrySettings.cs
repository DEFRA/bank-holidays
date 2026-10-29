namespace DEFRA.BankHolidays.Settings
{
    public static class TelemetrySettings
    {
        public const string AppSettingsKey = "ApplicationInsights:ConnectionString";

        // The standard variable that Azure hosting platforms use to configure Application Insights.
        public const string StandardKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

        /// <summary>Returns the Application Insights connection string, or null when telemetry is not configured.</summary>
        public static string ResolveConnectionString(IConfiguration configuration)
        {
            var value = configuration[AppSettingsKey];

            if (string.IsNullOrWhiteSpace(value))
            {
                value = configuration[StandardKey];
            }

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
