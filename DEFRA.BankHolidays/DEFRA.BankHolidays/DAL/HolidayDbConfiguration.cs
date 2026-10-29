using System.Data.Entity;
using System.Data.Entity.SqlServer;
using Microsoft.Data.SqlClient;

namespace DEFRA.BankHolidays.DAL
{
    // Code-based replacement for the <entityFramework> section of Web.config. Uses the Microsoft.Data.SqlClient
    // provider so that Entra ID authentication (for example "Authentication=Active Directory Default" with a
    // managed identity) can be selected purely through the connection string.
    public class HolidayDbConfiguration : DbConfiguration
    {
        public HolidayDbConfiguration()
        {
            SetProviderFactory(MicrosoftSqlProviderServices.ProviderInvariantName, SqlClientFactory.Instance);
            SetProviderServices(MicrosoftSqlProviderServices.ProviderInvariantName, MicrosoftSqlProviderServices.Instance);
            SetMigrationSqlGenerator(MicrosoftSqlProviderServices.ProviderInvariantName, () => new MicrosoftSqlServerMigrationSqlGenerator());
            SetExecutionStrategy(MicrosoftSqlProviderServices.ProviderInvariantName, () => new MicrosoftSqlAzureExecutionStrategy());
        }
    }
}
