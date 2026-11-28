using DEFRA.BankHolidays.DAL;
using DEFRA.BankHolidays.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DEFRA.BankHolidays.Tests
{
    // Real EF Core context over an in-memory SQLite database. The connection is kept open for the lifetime of
    // the instance because an in-memory SQLite database disappears when its connection closes.
    internal sealed class TestDb : IDisposable
    {
        private readonly SqliteConnection connection = new("DataSource=:memory:");

        public TestDb()
        {
            connection.Open();
            using var context = NewContext();
            context.Database.EnsureCreated();
        }

        public SqliteConnection Connection => connection;

        public DbContextOptions<HolidayContext> Options =>
            new DbContextOptionsBuilder<HolidayContext>().UseSqlite(connection).Options;

        public HolidayContext NewContext() => new(Options);

        // Always read from a fresh context so assertions see what was actually persisted.
        public List<Holidays> Data
        {
            get
            {
                using var context = NewContext();
                return context.Holidays
                    .AsNoTracking()
                    .Include(x => x.EnglandWales.Events)
                    .Include(x => x.NorthernIreland.Events)
                    .Include(x => x.Scotland.Events)
                    .AsSplitQuery()
                    .ToList();
            }
        }

        public void Add(Holidays holidays)
        {
            using var context = NewContext();
            context.Holidays.Add(holidays);
            context.SaveChanges();
        }

        public void Dispose() => connection.Dispose();
    }
}
