using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DEFRA.BankHolidays.Tests.Integration
{
    // The application must fail closed: without Entra ID configuration it may only start in Development,
    // and only when anonymous writes are explicitly allowed.
    [TestClass]
    public class StartupConfigurationTest
    {
        private static Exception StartAndCapture(AppFactory factory)
        {
            try
            {
                using var client = factory.CreateClient();
            }
            catch (Exception ex)
            {
                return ex is InvalidOperationException ? ex : ex.InnerException ?? ex;
            }

            return null;
        }

        [TestMethod]
        public void Production_without_entra_configuration_does_not_start()
        {
            using var factory = new AppFactory(environment: "Production", entraConfigured: false);

            var exception = StartAndCapture(factory);

            Assert.IsNotNull(exception, "The application started without Entra ID configuration in Production.");
            StringAssert.Contains(exception.Message, "Microsoft Entra ID is not configured");
        }

        [TestMethod]
        public void Development_does_not_start_without_entra_unless_anonymous_writes_are_enabled()
        {
            using var factory = new AppFactory(environment: "Development", entraConfigured: false, allowAnonymousWrites: false);

            var exception = StartAndCapture(factory);

            Assert.IsNotNull(exception, "The application started without Entra ID configuration and without the development switch.");
            StringAssert.Contains(exception.Message, "Microsoft Entra ID is not configured");
        }

        [TestMethod]
        public void Production_ignores_the_development_switch()
        {
            using var factory = new AppFactory(environment: "Production", entraConfigured: false, allowAnonymousWrites: true);

            var exception = StartAndCapture(factory);

            Assert.IsNotNull(exception, "The development switch was honoured in Production.");
            StringAssert.Contains(exception.Message, "Microsoft Entra ID is not configured");
        }

        [TestMethod]
        public void Production_starts_when_entra_is_configured()
        {
            using var factory = new AppFactory(environment: "Production", entraConfigured: true);

            var exception = StartAndCapture(factory);

            Assert.IsNull(exception, exception?.ToString());
        }
    }
}
