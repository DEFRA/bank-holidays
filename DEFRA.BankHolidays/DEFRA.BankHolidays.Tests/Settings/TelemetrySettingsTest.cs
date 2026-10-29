using DEFRA.BankHolidays.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DEFRA.BankHolidays.Tests.Settings
{
    [TestClass]
    public class TelemetrySettingsTest
    {
        private static IConfiguration Config(params (string Key, string Value)[] items)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(items.ToDictionary(x => x.Key, x => x.Value))
                .Build();
        }

        [TestMethod]
        public void Returns_null_when_telemetry_is_not_configured()
        {
            Assert.IsNull(TelemetrySettings.ResolveConnectionString(Config()));
        }

        [TestMethod]
        public void Returns_null_when_the_setting_is_blank()
        {
            var config = Config((TelemetrySettings.AppSettingsKey, "  "));

            Assert.IsNull(TelemetrySettings.ResolveConnectionString(config));
        }

        [TestMethod]
        public void Uses_the_application_setting()
        {
            var config = Config((TelemetrySettings.AppSettingsKey, "InstrumentationKey=app"));

            Assert.AreEqual("InstrumentationKey=app", TelemetrySettings.ResolveConnectionString(config));
        }

        [TestMethod]
        public void Uses_the_standard_azure_variable()
        {
            var config = Config((TelemetrySettings.StandardKey, "InstrumentationKey=standard"));

            Assert.AreEqual("InstrumentationKey=standard", TelemetrySettings.ResolveConnectionString(config));
        }

        [TestMethod]
        public void Prefers_the_application_setting_when_both_are_set()
        {
            var config = Config(
                (TelemetrySettings.AppSettingsKey, "InstrumentationKey=app"),
                (TelemetrySettings.StandardKey, "InstrumentationKey=standard"));

            Assert.AreEqual("InstrumentationKey=app", TelemetrySettings.ResolveConnectionString(config));
        }

        [TestMethod]
        public void Falls_back_to_the_standard_variable_when_the_application_setting_is_blank()
        {
            var config = Config(
                (TelemetrySettings.AppSettingsKey, ""),
                (TelemetrySettings.StandardKey, "InstrumentationKey=standard"));

            Assert.AreEqual("InstrumentationKey=standard", TelemetrySettings.ResolveConnectionString(config));
        }
    }
}
