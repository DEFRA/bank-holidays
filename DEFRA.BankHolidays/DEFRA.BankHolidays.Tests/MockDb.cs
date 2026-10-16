using DEFRA.BankHolidays.DAL;
using DEFRA.BankHolidays.Models;
using Moq;
using System.Data.Entity;

namespace DEFRA.BankHolidays.Tests
{
    // Replaces EntityFramework.Testing (not available for .NET 10): an in-memory DbSet/DbContext mock.
    internal static class MockDb
    {
        public static Mock<DbSet<Holidays>> CreateHolidaysSet(List<Holidays> data)
        {
            var queryable = data.AsQueryable();
            var set = new Mock<DbSet<Holidays>>();

            set.As<IQueryable<Holidays>>().Setup(m => m.Provider).Returns(queryable.Provider);
            set.As<IQueryable<Holidays>>().Setup(m => m.Expression).Returns(queryable.Expression);
            set.As<IQueryable<Holidays>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
            set.As<IQueryable<Holidays>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());
            set.Setup(m => m.Include(It.IsAny<string>())).Returns(set.Object);
            set.Setup(m => m.Add(It.IsAny<Holidays>())).Returns<Holidays>(h => { data.Add(h); return h; });
            set.Setup(m => m.RemoveRange(It.IsAny<IEnumerable<Holidays>>()))
               .Returns<IEnumerable<Holidays>>(items => { foreach (var item in items.ToList()) { data.Remove(item); } return items; });

            return set;
        }

        public static Mock<HolidayContext> CreateContext(Mock<DbSet<Holidays>> holidays)
        {
            // The connection string is never used: DbContext connects lazily and every member the tests touch is mocked.
            var context = new Mock<HolidayContext>("Data Source=unused");
            context.Setup(x => x.Holidays).Returns(holidays.Object);

            return context;
        }
    }
}
