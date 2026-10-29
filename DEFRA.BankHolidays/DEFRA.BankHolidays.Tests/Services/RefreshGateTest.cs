using DEFRA.BankHolidays.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DEFRA.BankHolidays.Tests.Services
{
    [TestClass]
    public class RefreshGateTest
    {
        [TestMethod]
        public void Allows_only_one_refresh_at_a_time()
        {
            var gate = new RefreshGate();

            using (var first = gate.TryAcquire())
            {
                Assert.IsNotNull(first);
                Assert.IsNull(gate.TryAcquire());
            }

            using var again = gate.TryAcquire();
            Assert.IsNotNull(again);
        }

        [TestMethod]
        public void Releasing_a_lease_twice_does_not_allow_two_refreshes()
        {
            var gate = new RefreshGate();
            var lease = gate.TryAcquire();

            lease.Dispose();
            lease.Dispose();

            using var next = gate.TryAcquire();
            Assert.IsNotNull(next);
            Assert.IsNull(gate.TryAcquire());
        }
    }
}
