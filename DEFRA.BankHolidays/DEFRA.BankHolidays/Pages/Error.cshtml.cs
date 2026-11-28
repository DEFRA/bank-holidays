using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DEFRA.BankHolidays.Pages
{
    // The exception handler re-executes the failed request, which may be a POST (Refresh, Update, ManualRefresh).
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        public void OnGet()
        {
        }

        public void OnPost()
        {
        }
    }
}
