using DEFRA.BankHolidays.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DEFRA.BankHolidays.Tests.Settings
{
    [TestClass]
    public class KeyVaultConfigurationTest
    {
        private static ConfigurationManager Configuration(string vaultUri)
        {
            var configuration = new ConfigurationManager();
            configuration.AddInMemoryCollection(new Dictionary<string, string> { [KeyVaultConfigurationExtensions.UriKey] = vaultUri });
            return configuration;
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow(null)]
        public void Adds_no_provider_when_no_vault_is_configured(string vaultUri)
        {
            var configuration = Configuration(vaultUri);
            var sourcesBefore = ((IConfigurationBuilder)configuration).Sources.Count;

            configuration.AddOptionalKeyVault();

            Assert.AreEqual(sourcesBefore, ((IConfigurationBuilder)configuration).Sources.Count);
        }

        [TestMethod]
        [DataRow("not a url")]
        [DataRow("http://myvault.vault.azure.net/")]
        [DataRow("myvault.vault.azure.net")]
        public void Rejects_a_vault_uri_that_is_not_an_absolute_https_url(string vaultUri)
        {
            var configuration = Configuration(vaultUri);

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => configuration.AddOptionalKeyVault());

            StringAssert.Contains(exception.Message, KeyVaultConfigurationExtensions.UriKey);
        }
    }
}
