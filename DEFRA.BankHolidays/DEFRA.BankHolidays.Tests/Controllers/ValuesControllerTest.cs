using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DEFRA.BankHolidays;
using DEFRA.BankHolidays.Controllers;
using DEFRA.BankHolidays.Models;
using Moq;
using DEFRA.BankHolidays.DAL;
using System.Data.Entity;

namespace DEFRA.BankHolidays.Tests.Controllers
{
    [TestClass]
    public class ValuesControllerTest
    {
        Mock<HolidayContext> context;
        Mock<DbSet<Holidays>> holidays;
        List<Holidays> holidaysData;
        ValuesController controller;

        [TestInitialize]
        public void Initialize()
        {
            holidaysData = new List<Holidays>
            {
                 new Holidays
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
                }

            };

            holidays = new Mock<DbSet<Holidays>>().SetupData(holidaysData);
            context = new Mock<HolidayContext>();
            context.Setup(x => x.Holidays).Returns(holidays.Object);
            controller = new ValuesController(context.Object);
        }

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
    }
}
