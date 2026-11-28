using DEFRA.BankHolidays.Controllers;
using DEFRA.BankHolidays.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DEFRA.BankHolidays.Tests.Controllers
{
    [TestClass]
    public class ValuesControllerTest
    {
        TestDb db;
        ValuesController controller;

        [TestInitialize]
        public void Initialize()
        {
            db = new TestDb();
            db.Add(new Holidays
            {
                EnglandWales = new EnglandWales
                {
                    Events = new List<Event>
                    {
                        new Event
                        {
                            Date = new DateTime(2018, 5, 28)
                        }
                    }
                }
            });

            controller = new ValuesController(db.NewContext());
        }

        [TestCleanup]
        public void Cleanup() => db.Dispose();

        [TestMethod]
        public void Test_IsBankHoliday_returns_true_when_bank_holiday()
        {
            Assert.IsTrue(controller.IsBankHoliday(new DateTime(2018, 5, 28)));
        }

        [TestMethod]
        public void Test_IsBankHoliday_returns_false_when_not_bank_holiday()
        {
            Assert.IsFalse(controller.IsBankHoliday(new DateTime(2018, 5, 27)));
        }

        [TestMethod]
        public void Test_WorkingDays_excludes_weekends_and_bank_holidays()
        {
            // Fri 25 May 2018 -> Tue 29 May 2018: Sat, Sun and Mon (bank holiday) are skipped, only Tue counts
            Assert.AreEqual(1, controller.WorkingDays(new DateTime(2018, 5, 25), new DateTime(2018, 5, 29)));
        }

        [TestMethod]
        public void Test_WorkingDays_counts_first_date_when_requested_and_not_a_bank_holiday()
        {
            Assert.AreEqual(2, controller.WorkingDays(new DateTime(2018, 5, 25), new DateTime(2018, 5, 29), true));
            Assert.AreEqual(1, controller.WorkingDays(new DateTime(2018, 5, 28), new DateTime(2018, 5, 29), true));
        }

        [TestMethod]
        public void Test_Get_selects_behaviour_from_query_parameters()
        {
            var isHoliday = (OkObjectResult)controller.Get(date: new DateTime(2018, 5, 28));
            var workingDays = (OkObjectResult)controller.Get(start: new DateTime(2018, 5, 25), end: new DateTime(2018, 5, 29));
            var all = (OkObjectResult)controller.Get();

            Assert.AreEqual(true, isHoliday.Value);
            Assert.AreEqual(1, workingDays.Value);
            Assert.IsInstanceOfType<Holidays>(all.Value);
        }
    }
}
