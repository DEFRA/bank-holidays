using DEFRA.BankHolidays.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DEFRA.BankHolidays.Tests.Settings
{
    [TestClass]
    public class HolidaySourceOptionsTest
    {
        [TestMethod]
        public void Defaults_are_valid_and_use_the_gov_uk_feed()
        {
            var options = new HolidaySourceOptions();

            Assert.AreEqual("https://www.gov.uk/bank-holidays.json", options.SourceUrl);
            Assert.AreEqual(30, options.SourceTimeoutSeconds);
            Assert.IsTrue(HolidaySourceOptions.IsValid(options));
        }

        [TestMethod]
        [DataRow("https://www.gov.uk/bank-holidays.json", 30, true)]
        [DataRow("http://localhost:5000/feed", 5, true)]
        [DataRow("ftp://example.org/feed", 30, false)]
        [DataRow("not a url", 30, false)]
        [DataRow("", 30, false)]
        [DataRow("https://www.gov.uk/bank-holidays.json", 0, false)]
        [DataRow("https://www.gov.uk/bank-holidays.json", -1, false)]
        public void Validates_url_and_timeout(string url, int timeoutSeconds, bool expected)
        {
            var options = new HolidaySourceOptions { SourceUrl = url, SourceTimeoutSeconds = timeoutSeconds };

            Assert.AreEqual(expected, HolidaySourceOptions.IsValid(options));
        }

        [TestMethod]
        public void Binds_from_configuration()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["BankHolidays:SourceUrl"] = "https://example.org/holidays.json",
                    ["BankHolidays:SourceTimeoutSeconds"] = "12"
                })
                .Build();

            var options = config.GetSection(HolidaySourceOptions.SectionName).Get<HolidaySourceOptions>();

            Assert.AreEqual("https://example.org/holidays.json", options.SourceUrl);
            Assert.AreEqual(12, options.SourceTimeoutSeconds);
        }
    }
}
