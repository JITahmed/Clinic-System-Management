using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using ClinicSystem.Reporting.DTOs;
using System.Text.Json;

namespace ClinicSystem.Reporting.Controllers
{
    [AuthorizeReporting]  // Custom filter - check JWT in session
    public class ReportsController : Controller
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;

        public ReportsController(IHttpClientFactory httpFactory, IConfiguration config)
        {
            _httpFactory = httpFactory;
            _config = config;
        }

        public async Task<IActionResult> Dashboard()
        {
            var client = CreateAuthorizedClient();
            var stats = await client.GetFromJsonAsync<List<StatusCount>>($"{_config["ApiSettings:BaseUrl"]}/api/reports/stats");
            return View(stats);
        }

        public async Task<IActionResult> DoctorUtilization()
        {
            var client = CreateAuthorizedClient();
            var doctors = await client.GetFromJsonAsync<List<DoctorDto>>($"{_config["ApiSettings:BaseUrl"]}/api/doctors");
            return View(doctors);
        }

        public async Task<IActionResult> CancellationRates()
        {
            var client = CreateAuthorizedClient();
            var rates = await client.GetFromJsonAsync<CancellationData>($"{_config["ApiSettings:BaseUrl"]}/api/reports/cancellations");
            return View(rates);
        }

        private HttpClient CreateAuthorizedClient()
        {
            var client = _httpFactory.CreateClient();
            var token = HttpContext.Session.GetString("jwt");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }
    }
}
