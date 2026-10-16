using DEFRA.BankHolidays.DAL;
using DEFRA.BankHolidays.Models;
using Microsoft.AspNetCore.Mvc;
using System.Data.Entity;

namespace DEFRA.BankHolidays.Controllers
{
    /// <summary>
    /// Bank holiday data and calculations. The behaviour is selected by the query string, as in the original Web API:
    /// no parameters returns all holidays, <c>date</c> returns whether it is a bank holiday,
    /// and <c>start</c> + <c>end</c> returns the number of working days between them.
    /// </summary>
    [ApiController]
    [Route("api/values")]
    public class ValuesController : ControllerBase
    {
        private readonly HolidayContext db;

        public ValuesController(HolidayContext context)
        {
            db = context;
        }

        /// <summary>Gets bank holidays, checks a date, or counts working days.</summary>
        /// <param name="date">When supplied, returns <c>true</c> if the date is an England and Wales bank holiday.</param>
        /// <param name="start">With <paramref name="end"/>, returns the number of working days after <paramref name="start"/> up to and including <paramref name="end"/>.</param>
        /// <param name="end">End of the working-day range.</param>
        /// <param name="includeFirstDate">Counts <paramref name="start"/> as a working day when it is not a bank holiday.</param>
        [HttpGet]
        [Produces("application/json")]
        public IActionResult Get(DateTime? date = null, DateTime? start = null, DateTime? end = null, bool includeFirstDate = false)
        {
            if (date.HasValue)
            {
                return Ok(IsBankHoliday(date.Value));
            }

            if (start.HasValue && end.HasValue)
            {
                return Ok(WorkingDays(start.Value, end.Value, includeFirstDate));
            }

            return Ok(GetHolidays());
        }

        [NonAction]
        public Holidays GetHolidays()
        {
            return db.Holidays.Include(x => x.EnglandWales.Events).Include(x => x.NorthernIreland.Events).Include(x => x.Scotland.Events).FirstOrDefault();
        }

        [NonAction]
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

        [NonAction]
        public bool IsBankHoliday(DateTime date)
        {
            return db.Holidays.Where(x => x.EnglandWales.Events.Any(z => z.Date == date)).FirstOrDefault() != null ? true : false;
        }
    }
}
