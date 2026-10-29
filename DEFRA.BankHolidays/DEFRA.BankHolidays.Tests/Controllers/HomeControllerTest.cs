using DEFRA.BankHolidays.Controllers;
using DEFRA.BankHolidays.DAL;
using DEFRA.BankHolidays.Models;
using DEFRA.BankHolidays.Services;
using DEFRA.BankHolidays.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DEFRA.BankHolidays.Tests.Controllers
{
    [TestClass]
    public class HomeControllerTest
    {
        internal const string SampleJson = @"{
            ""england-and-wales"": { ""division"": ""england-and-wales"", ""events"": [ { ""title"": ""Christmas Day"", ""date"": ""2018-12-25"", ""notes"": """", ""bunting"": true } ] },
            ""scotland"": { ""division"": ""scotland"", ""events"": [] },
            ""northern-ireland"": { ""division"": ""northern-ireland"", ""events"": [] }
        }";

        private static HomeController CreateController(HolidayContext context, HttpMessageHandler handler = null, RefreshGate gate = null)
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(x => x.CreateClient(HomeController.SourceClientName))
                   .Returns(() => new HttpClient(handler ?? new StubHandler(SampleJson)));

            return new HomeController(
                context,
                factory.Object,
                Options.Create(new HolidaySourceOptions()),
                gate ?? new RefreshGate(),
                NullLogger<HomeController>.Instance);
        }

        [TestMethod]
        public void Refresh_replaces_existing_holidays_and_saves()
        {
            var data = new List<Holidays> { new Holidays { Id = 1 } };
            var context = MockDb.CreateContext(MockDb.CreateHolidaysSet(data), data);
            var controller = CreateController(context.Object);

            var result = controller.Refresh(SampleJson) as ContentResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Ok", result.Content);
            Assert.AreEqual(1, data.Count);
            Assert.AreEqual("england-and-wales", data[0].EnglandWales.Division);
            Assert.AreEqual(new DateTime(2018, 12, 25), data[0].EnglandWales.Events[0].Date);
            context.Verify(x => x.ReplaceHolidays(It.IsAny<Holidays>()), Times.Once);
        }

        [TestMethod]
        public void Refresh_returns_generic_bad_request_for_invalid_data()
        {
            var context = MockDb.CreateContext(MockDb.CreateHolidaysSet(new List<Holidays>()));
            var controller = CreateController(context.Object);

            var result = controller.Refresh("not json") as BadRequestObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("The supplied bank holiday data is not valid.", result.Value);
            context.Verify(x => x.ReplaceHolidays(It.IsAny<Holidays>()), Times.Never);
        }

        [TestMethod]
        public void Refresh_returns_bad_request_for_json_null()
        {
            var context = MockDb.CreateContext(MockDb.CreateHolidaysSet(new List<Holidays>()));
            var controller = CreateController(context.Object);

            var result = controller.Refresh("null");

            Assert.IsInstanceOfType<BadRequestObjectResult>(result);
            context.Verify(x => x.ReplaceHolidays(It.IsAny<Holidays>()), Times.Never);
        }

        [TestMethod]
        public void Refresh_does_not_expose_exception_details_when_saving_fails()
        {
            var context = MockDb.CreateContext(MockDb.CreateHolidaysSet(new List<Holidays>()));
            context.Setup(x => x.ReplaceHolidays(It.IsAny<Holidays>())).Throws(new InvalidOperationException("server=secret-host"));
            var controller = CreateController(context.Object);

            var result = controller.Refresh(SampleJson) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(500, result.StatusCode);
            Assert.IsFalse(result.Value.ToString().Contains("secret-host"));
        }

        [TestMethod]
        public void Refresh_returns_conflict_while_another_refresh_is_running()
        {
            var context = MockDb.CreateContext(MockDb.CreateHolidaysSet(new List<Holidays>()));
            var gate = new RefreshGate();
            var controller = CreateController(context.Object, gate: gate);

            using (gate.TryAcquire())
            {
                var result = controller.Refresh(SampleJson) as ObjectResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(409, result.StatusCode);
            }

            context.Verify(x => x.ReplaceHolidays(It.IsAny<Holidays>()), Times.Never);
        }

        [TestMethod]
        public async Task Update_replaces_holidays_with_data_from_the_source()
        {
            var data = new List<Holidays> { new Holidays { Id = 1 } };
            var context = MockDb.CreateContext(MockDb.CreateHolidaysSet(data), data);
            var controller = CreateController(context.Object);

            var result = await controller.Update(CancellationToken.None) as StatusCodeResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(200, result.StatusCode);
            Assert.AreEqual(1, data.Count);
            Assert.AreEqual("england-and-wales", data[0].EnglandWales.Division);
            context.Verify(x => x.ReplaceHolidays(It.IsAny<Holidays>()), Times.Once);
        }

        [TestMethod]
        public async Task Update_returns_bad_gateway_when_the_source_is_unavailable()
        {
            var context = MockDb.CreateContext(MockDb.CreateHolidaysSet(new List<Holidays>()));
            var controller = CreateController(context.Object, new StubHandler(new HttpRequestException("connection refused")));

            var result = await controller.Update(CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(502, result.StatusCode);
            Assert.IsFalse(result.Value.ToString().Contains("connection refused"));
            context.Verify(x => x.ReplaceHolidays(It.IsAny<Holidays>()), Times.Never);
        }

        [TestMethod]
        public async Task Update_returns_bad_gateway_when_the_source_times_out()
        {
            var context = MockDb.CreateContext(MockDb.CreateHolidaysSet(new List<Holidays>()));
            var controller = CreateController(context.Object, new StubHandler(new TaskCanceledException("timed out")));

            var result = await controller.Update(CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(502, result.StatusCode);
        }

        [TestMethod]
        public async Task Update_returns_bad_gateway_when_the_source_returns_invalid_data()
        {
            var data = new List<Holidays> { new Holidays { Id = 1 } };
            var context = MockDb.CreateContext(MockDb.CreateHolidaysSet(data), data);
            var controller = CreateController(context.Object, new StubHandler("<html>not json</html>"));

            var result = await controller.Update(CancellationToken.None) as ObjectResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(502, result.StatusCode);
            Assert.AreEqual(1, data[0].Id);
            context.Verify(x => x.ReplaceHolidays(It.IsAny<Holidays>()), Times.Never);
        }

        [TestMethod]
        public async Task Update_returns_conflict_while_another_refresh_is_running()
        {
            var context = MockDb.CreateContext(MockDb.CreateHolidaysSet(new List<Holidays>()));
            var gate = new RefreshGate();
            var controller = CreateController(context.Object, gate: gate);

            using (gate.TryAcquire())
            {
                var result = await controller.Update(CancellationToken.None) as ObjectResult;

                Assert.IsNotNull(result);
                Assert.AreEqual(409, result.StatusCode);
            }
        }
    }
}
