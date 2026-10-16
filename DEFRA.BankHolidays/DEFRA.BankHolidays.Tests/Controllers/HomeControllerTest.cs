using DEFRA.BankHolidays.Controllers;
using DEFRA.BankHolidays.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DEFRA.BankHolidays.Tests.Controllers
{
    [TestClass]
    public class HomeControllerTest
    {
        private const string SampleJson = @"{
            ""england-and-wales"": { ""division"": ""england-and-wales"", ""events"": [ { ""title"": ""Christmas Day"", ""date"": ""2018-12-25"", ""notes"": """", ""bunting"": true } ] },
            ""scotland"": { ""division"": ""scotland"", ""events"": [] },
            ""northern-ireland"": { ""division"": ""northern-ireland"", ""events"": [] }
        }";

        [TestMethod]
        public void Refresh_replaces_existing_holidays_and_saves()
        {
            var data = new List<Holidays> { new Holidays { Id = 1 } };
            var set = MockDb.CreateHolidaysSet(data);
            var context = MockDb.CreateContext(set);
            var controller = new HomeController(context.Object, Mock.Of<IHttpClientFactory>());

            var result = controller.Refresh(SampleJson) as ContentResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Ok", result.Content);
            Assert.AreEqual(1, data.Count);
            Assert.AreEqual("england-and-wales", data[0].EnglandWales.Division);
            Assert.AreEqual(new DateTime(2018, 12, 25), data[0].EnglandWales.Events[0].Date);
            context.Verify(x => x.SaveChanges(), Times.Once);
        }

        [TestMethod]
        public void Refresh_returns_error_message_for_invalid_data()
        {
            var context = MockDb.CreateContext(MockDb.CreateHolidaysSet(new List<Holidays>()));
            var controller = new HomeController(context.Object, Mock.Of<IHttpClientFactory>());

            var result = controller.Refresh("not json") as ContentResult;

            Assert.IsNotNull(result);
            Assert.AreNotEqual("Ok", result.Content);
            context.Verify(x => x.SaveChanges(), Times.Never);
        }
    }
}
