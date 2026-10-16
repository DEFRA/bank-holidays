using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.SqlServer;

namespace DEFRA.BankHolidays.DAL
{
    // Code-based replacement for the <entityFramework> section of Web.config.
    public class HolidayDbConfiguration : DbConfiguration
    {
        public HolidayDbConfiguration()
        {
            SetProviderServices("System.Data.SqlClient", SqlProviderServices.Instance);
            SetDefaultConnectionFactory(new SqlConnectionFactory());
        }
    }
}
