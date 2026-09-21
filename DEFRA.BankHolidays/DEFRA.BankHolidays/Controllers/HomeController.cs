using DEFRA.BankHolidays.DAL;
using DEFRA.BankHolidays.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace DEFRA.BankHolidays.Controllers
{
    public class HomeController : Controller
    {
        HolidayContext db;

        public HomeController()
        {
            db = new HolidayContext();
        }

        public HomeController(HolidayContext context)
        {
            db = context;
        }

        public ActionResult Index()
        {
            ViewBag.Title = "Bank Holidays";

            return View();
        }

        [HttpPost]
        public ActionResult Refresh(string data)
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
        public ActionResult Update()
        {
            using (WebClient client = new WebClient())
            {
                Update(client.DownloadString("https://www.gov.uk/bank-holidays.json"));
            }

            return new HttpStatusCodeResult(200);
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
