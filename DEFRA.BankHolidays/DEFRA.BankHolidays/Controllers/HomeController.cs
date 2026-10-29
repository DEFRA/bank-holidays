using DEFRA.BankHolidays.DAL;
using DEFRA.BankHolidays.Models;
using DEFRA.BankHolidays.Services;
using DEFRA.BankHolidays.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace DEFRA.BankHolidays.Controllers
{
    public class HomeController : Controller
    {
        public const string SourceClientName = "bank-holidays-source";

        private readonly HolidayContext db;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly HolidaySourceOptions sourceOptions;
        private readonly RefreshGate refreshGate;
        private readonly ILogger<HomeController> logger;

        public HomeController(
            HolidayContext context,
            IHttpClientFactory httpClientFactory,
            IOptions<HolidaySourceOptions> sourceOptions,
            RefreshGate refreshGate,
            ILogger<HomeController> logger)
        {
            db = context;
            this.httpClientFactory = httpClientFactory;
            this.sourceOptions = sourceOptions.Value;
            this.refreshGate = refreshGate;
            this.logger = logger;
        }

        public IActionResult Index()
        {
            ViewBag.Title = "Bank Holidays";

            return View();
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.Refresh)]
        public IActionResult Refresh(string data)
        {
            using var lease = refreshGate.TryAcquire();

            if (lease == null)
            {
                return StatusCode(StatusCodes.Status409Conflict, "A refresh is already in progress.");
            }

            try
            {
                Update(data);
                return Content("Ok");
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Refresh rejected: the supplied data is not valid bank holiday JSON.");
                return BadRequest("The supplied bank holiday data is not valid.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Refresh failed while saving bank holiday data.");
                return StatusCode(StatusCodes.Status500InternalServerError, "The bank holiday data could not be refreshed.");
            }
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.Refresh)]
        public async Task<IActionResult> Update(CancellationToken cancellationToken)
        {
            using var lease = refreshGate.TryAcquire();

            if (lease == null)
            {
                return StatusCode(StatusCodes.Status409Conflict, "A refresh is already in progress.");
            }

            string data;

            try
            {
                var client = httpClientFactory.CreateClient(SourceClientName);
                data = await client.GetStringAsync(sourceOptions.SourceUrl, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                logger.LogError(ex, "Refresh failed: the bank holiday source could not be read.");
                return StatusCode(StatusCodes.Status502BadGateway, "The bank holiday source is unavailable.");
            }

            try
            {
                Update(data);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Refresh failed: the bank holiday source returned data that is not valid.");
                return StatusCode(StatusCodes.Status502BadGateway, "The bank holiday source returned invalid data.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Refresh failed while saving bank holiday data.");
                return StatusCode(StatusCodes.Status500InternalServerError, "The bank holiday data could not be refreshed.");
            }

            return StatusCode(200);
        }

        public IActionResult Error()
        {
            return View();
        }

        private void Update(string data)
        {
            Holidays holidays = JsonConvert.DeserializeObject<Holidays>(data)
                ?? throw new JsonSerializationException("The bank holiday data is empty.");

            db.ReplaceHolidays(holidays);
        }
    }
}
