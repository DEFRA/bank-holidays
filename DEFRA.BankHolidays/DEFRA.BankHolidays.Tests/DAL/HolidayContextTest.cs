using DEFRA.BankHolidays.DAL;
using Microsoft.Data.SqlClient;
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
            using var context = new HolidayContext("Data Source=unused; Initial Catalog=unused; Authentication=Active Directory Default; Encrypt=True");

            Assert.IsInstanceOfType<SqlConnection>(context.Database.Connection);

            var builder = new SqlConnectionStringBuilder(context.Database.Connection.ConnectionString);
            Assert.AreEqual(SqlAuthenticationMethod.ActiveDirectoryDefault, builder.Authentication);
        }
    }
}
