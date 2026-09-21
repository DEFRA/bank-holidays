using System.Web;
using System.Web.Mvc;

namespace DEFRA.BankHolidays
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}
