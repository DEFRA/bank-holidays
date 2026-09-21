using DEFRA.BankHolidays.DAL;
using DEFRA.BankHolidays.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Data.Entity;

namespace DEFRA.BankHolidays.Controllers
{
    public class ValuesController : ApiController
    {
        HolidayContext db;

        public ValuesController()
        {
            db = new HolidayContext();
        }

        public ValuesController(HolidayContext context)
        {
            db = context;
        }

        // GET api/values        
        public Holidays Get()
        {
            return db.Holidays.Include(x => x.EnglandWales.Events).Include(x => x.NorthernIreland.Events).Include(x => x.Scotland.Events).FirstOrDefault();
        }

        [HttpGet]
        public int WorkingDays(DateTime start, DateTime end, bool includeFirstDate = false)
        {
            var total_days = 0;
            var holidays = db.Holidays.Include(x => x.EnglandWales.Events).FirstOrDefault()?.EnglandWales?.Events.Select(x => x.Date).ToList();

            if (holidays != null)
            {
                for (var date = start.AddDays(1); date <= end; date = date.AddDays(1))
                {
                    if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday && !holidays.Contains(date))
                    {
                        total_days++;
                    }
                }

                if (total_days < 0)
                {
                    total_days = 0;
                }                
            }

            if (includeFirstDate && !IsBankHoliday(start))
            {
                total_days++;
            }

            return total_days;
        }        

        [HttpGet]
        public bool IsBankHoliday(DateTime date)
        {
            return  db.Holidays.Where(x => x.EnglandWales.Events.Any(z => z.Date == date)).FirstOrDefault() != null ? true : false;
        }
    }
}
