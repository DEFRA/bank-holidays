using DEFRA.BankHolidays.Models;
using Microsoft.Data.SqlClient;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;

namespace DEFRA.BankHolidays.DAL
{
    [DbConfigurationType(typeof(HolidayDbConfiguration))]
    public class HolidayContext : DbContext
    {
        static HolidayContext()
        {
            // The schema is applied explicitly (migration utilities / deployment pipeline), never implicitly at runtime.
            Database.SetInitializer<HolidayContext>(null);
        }

        // Required by DbMigrator, which creates the context itself; the migration configuration supplies the real connection.
        public HolidayContext() : base("HolidayContext")
        {
        }

        // The connection is created explicitly so the Microsoft.Data.SqlClient provider is always used.
        public HolidayContext(string connectionString) : base(new SqlConnection(connectionString), true)
        {
        }

        public virtual DbSet<Holidays> Holidays { get; set; }

        /// <summary>
        /// Replaces all bank holiday data with <paramref name="holidays"/> in a single transaction. The division and
        /// event rows are the principals of the Holidays row's foreign keys, so they are not removed with it and
        /// must be deleted explicitly, otherwise every refresh would leave the previous rows behind.
        /// </summary>
        public virtual void ReplaceHolidays(Holidays holidays)
        {
            Set<Event>().RemoveRange(Set<Event>());
            Set<EnglandWales>().RemoveRange(Set<EnglandWales>());
            Set<Scotland>().RemoveRange(Set<Scotland>());
            Set<NorthernIreland>().RemoveRange(Set<NorthernIreland>());
            Holidays.RemoveRange(Holidays);
            Holidays.Add(holidays);

            SaveChanges();
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
        }
    }
}
