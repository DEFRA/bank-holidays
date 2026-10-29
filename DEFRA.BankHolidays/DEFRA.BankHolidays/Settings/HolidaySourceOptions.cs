namespace DEFRA.BankHolidays.Settings
{
    /// <summary>Where bank holiday data is fetched from when the data is refreshed.</summary>
    public class HolidaySourceOptions
    {
        public const string SectionName = "BankHolidays";

        public string SourceUrl { get; set; } = "https://www.gov.uk/bank-holidays.json";

        public int SourceTimeoutSeconds { get; set; } = 30;

        public static bool IsValid(HolidaySourceOptions options)
        {
            return Uri.TryCreate(options.SourceUrl, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                && options.SourceTimeoutSeconds > 0;
        }
    }
}
