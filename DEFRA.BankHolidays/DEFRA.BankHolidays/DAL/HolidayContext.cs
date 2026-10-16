using DEFRA.BankHolidays.Models;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;

namespace DEFRA.BankHolidays.DAL
{
    [DbConfigurationType(typeof(HolidayDbConfiguration))]
    public class HolidayContext : DbContext
    {
        // Required by DbMigrator, which creates the context itself; the migration configuration supplies the real connection.
        public HolidayContext() : base("HolidayContext")
        {
        }

        public HolidayContext(string nameOrConnectionString) : base(nameOrConnectionString)
        {
        }

        public virtual DbSet<Holidays> Holidays { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
        }
    }
}
