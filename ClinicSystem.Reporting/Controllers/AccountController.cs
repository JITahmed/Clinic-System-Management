using Microsoft.AspNetCore.Mvc;
using ClinicSystem.Reporting.DTOs;
using System.Text.Json;

namespace ClinicSystem.Reporting.Controllers
{
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;

        public AccountController(IHttpClientFactory httpFactory, IConfiguration config)
        {
            _httpFactory = httpFactory;
            _config = config;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var client = _httpFactory.CreateClient();
            var response = await client.PostAsJsonAsync($"{_config["ApiSettings:BaseUrl"]}/api/auth/login", new { email, password });

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<LoginResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                HttpContext.Session.SetString("jwt", result.token);
                HttpContext.Session.SetString("userEmail", result.email);
                return RedirectToAction("Dashboard", "Reports");
            }

            ViewBag.Error = "Invalid credentials";
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
