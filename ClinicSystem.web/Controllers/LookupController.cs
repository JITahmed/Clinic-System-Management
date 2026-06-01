using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClinicSystem.web.DTOs;
using ClinicSystem.Api.DTOs;
using System.Text.Json;                  

namespace ClinicSystem.web.Controllers
{
    [AllowAnonymous]
    public class LookupController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public LookupController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public IActionResult Index() => View();

        [HttpPost]
        public async Task<IActionResult> Index(string cpr, string referenceNumber)
        {
            var client = _httpClientFactory.CreateClient();
            var apiBaseUrl = _config["ApiSettings:BaseUrl"] ?? "https://localhost:7001";

            var response = await client.GetAsync($"{apiBaseUrl}/api/appointments/lookup?cpr={cpr}&ref={referenceNumber}");

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<PublicLookupResponse>();
                return View("Result", data);
            }

            ViewBag.Error = "No appointments found. Please check your CPR and Reference Number.";
            return View();
        }
    }
}
