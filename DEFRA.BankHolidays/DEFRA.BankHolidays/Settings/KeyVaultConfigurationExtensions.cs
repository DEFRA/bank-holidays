using Azure.Identity;

namespace DEFRA.BankHolidays.Settings
{
    public static class KeyVaultConfigurationExtensions
    {
        public const string UriKey = "KeyVault:Uri";

        /// <summary>
        /// Adds Key Vault secrets as configuration when <c>KeyVault:Uri</c> is set, authenticating with
        /// <see cref="DefaultAzureCredential"/> (managed identity in Azure). Secret names use "--" for ":"
        /// (for example <c>ConnectionStrings--HolidayContext</c>). Does nothing when the setting is empty.
        /// </summary>
        public static void AddOptionalKeyVault(this ConfigurationManager configuration)
        {
            var value = configuration[UriKey];

            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var vaultUri) || vaultUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException($"'{UriKey}' must be an absolute https URL.");
            }

            configuration.AddAzureKeyVault(vaultUri, new DefaultAzureCredential());
        }
    }
}
