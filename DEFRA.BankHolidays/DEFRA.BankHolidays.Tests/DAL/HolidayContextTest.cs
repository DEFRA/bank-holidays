using DEFRA.BankHolidays.DAL;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DEFRA.BankHolidays.Tests.DAL
{
    [TestClass]
    public class HolidayContextTest
    {
        [TestMethod]
        public void Uses_the_Microsoft_Data_SqlClient_provider_so_entra_authentication_is_available()
        {
            // No connection is opened; this only checks which provider the context is wired to.
            var options = new DbContextOptionsBuilder<HolidayContext>()
                .UseSqlServer("Data Source=unused; Initial Catalog=unused; Authentication=Active Directory Default; Encrypt=True")
                .Options;
            using var context = new HolidayContext(options);

            var connection = context.Database.GetDbConnection();
            Assert.IsInstanceOfType<SqlConnection>(connection);

            var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
            Assert.AreEqual(SqlAuthenticationMethod.ActiveDirectoryDefault, builder.Authentication);
        }
    }
}
