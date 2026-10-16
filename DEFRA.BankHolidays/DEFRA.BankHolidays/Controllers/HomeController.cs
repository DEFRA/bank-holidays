using DEFRA.BankHolidays.DAL;
using DEFRA.BankHolidays.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace DEFRA.BankHolidays.Controllers
{
    public class HomeController : Controller
    {
        private const string GovUkBankHolidaysUrl = "https://www.gov.uk/bank-holidays.json";

        private readonly HolidayContext db;
        private readonly IHttpClientFactory httpClientFactory;

        public HomeController(HolidayContext context, IHttpClientFactory httpClientFactory)
        {
            db = context;
            this.httpClientFactory = httpClientFactory;
        }

        public IActionResult Index()
        {
            ViewBag.Title = "Bank Holidays";

            return View();
        }

        [HttpPost]
        public IActionResult Refresh(string data)
        {
            try
            {
                Update(data);
                return Content("Ok");
            }
            catch (Exception ex)
            {
                return Content(ex.Message.ToString());
            }
        }

        [HttpPost]
        public async Task<IActionResult> Update()
        {
            var client = httpClientFactory.CreateClient();

            Update(await client.GetStringAsync(GovUkBankHolidaysUrl));

            return StatusCode(200);
        }

        public IActionResult Error()
        {
            return View();
        }

        private void Update(string data)
        {
            Holidays holidays = JsonConvert.DeserializeObject<Holidays>(data);

            var existing = db.Holidays;

            db.Holidays.RemoveRange(existing);
            db.Holidays.Add(holidays);

            db.SaveChanges();
        }
    }
}
