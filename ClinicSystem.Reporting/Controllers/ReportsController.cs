using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using ClinicSystem.Reporting.DTOs;
using System.Text.Json;

namespace ClinicSystem.Reporting.Controllers
{
    [AuthorizeReporting]
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
            var response = await client.GetAsync($"{_config["ApiSettings:BaseUrl"]}/api/reports/stats");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            response.EnsureSuccessStatusCode();
            var stats = await response.Content.ReadFromJsonAsync<List<StatusCount>>();
            return View(stats);
        }

        public async Task<IActionResult> DoctorUtilization()
        {
            var client = CreateAuthorizedClient();
            var response = await client.GetAsync($"{_config["ApiSettings:BaseUrl"]}/api/doctors");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            response.EnsureSuccessStatusCode();
            var doctors = await response.Content.ReadFromJsonAsync<List<DoctorDto>>();
            return View(doctors);
        }

        public async Task<IActionResult> CancellationRates()
        {
            var client = CreateAuthorizedClient();
            var response = await client.GetAsync($"{_config["ApiSettings:BaseUrl"]}/api/reports/cancellations");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            response.EnsureSuccessStatusCode();
            var rates = await response.Content.ReadFromJsonAsync<CancellationData>();
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