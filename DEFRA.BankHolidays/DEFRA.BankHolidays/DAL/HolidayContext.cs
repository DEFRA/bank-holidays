using DEFRA.BankHolidays.Models;
using Microsoft.EntityFrameworkCore;

namespace DEFRA.BankHolidays.DAL
{
    public class HolidayContext : DbContext
    {
        public HolidayContext(DbContextOptions<HolidayContext> options) : base(options)
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
            Set<Event>().RemoveRange(Set<Event>().ToList());
            Set<EnglandWales>().RemoveRange(Set<EnglandWales>().ToList());
            Set<Scotland>().RemoveRange(Set<Scotland>().ToList());
            Set<NorthernIreland>().RemoveRange(Set<NorthernIreland>().ToList());
            Holidays.RemoveRange(Holidays.ToList());
            Holidays.Add(holidays);

            SaveChanges();
        }

        // Table and column names match the schema created by the previous EF6 model (singular table names,
        // <Navigation>_Id foreign key columns, datetime for dates), so existing databases keep working unchanged.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Holidays>(entity =>
            {
                entity.ToTable("Holidays");
                entity.HasOne(x => x.EnglandWales).WithMany().HasForeignKey("EnglandWales_Id").OnDelete(DeleteBehavior.ClientSetNull);
                entity.HasOne(x => x.NorthernIreland).WithMany().HasForeignKey("NorthernIreland_Id").OnDelete(DeleteBehavior.ClientSetNull);
                entity.HasOne(x => x.Scotland).WithMany().HasForeignKey("Scotland_Id").OnDelete(DeleteBehavior.ClientSetNull);
            });

            modelBuilder.Entity<EnglandWales>(entity =>
            {
                entity.ToTable("EnglandWales");
                entity.HasMany(x => x.Events).WithOne().HasForeignKey("EnglandWales_Id").OnDelete(DeleteBehavior.ClientSetNull);
            });

            modelBuilder.Entity<Scotland>(entity =>
            {
                entity.ToTable("Scotland");
                entity.HasMany(x => x.Events).WithOne().HasForeignKey("Scotland_Id").OnDelete(DeleteBehavior.ClientSetNull);
            });

            modelBuilder.Entity<NorthernIreland>(entity =>
            {
                entity.ToTable("NorthernIreland");
                entity.HasMany(x => x.Events).WithOne().HasForeignKey("NorthernIreland_Id").OnDelete(DeleteBehavior.ClientSetNull);
            });

            modelBuilder.Entity<Event>(entity =>
            {
                entity.ToTable("Event");
                entity.Property(x => x.Date).HasColumnType("datetime");
            });
        }
    }
}
