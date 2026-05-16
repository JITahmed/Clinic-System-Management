using Microsoft.AspNetCore.Mvc;

namespace ClinicSystem.web.Controllers
{
    public class PatientController : Controller
    {
        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult Profile()
        {
            return View();
        }

        public IActionResult BookAppointment()
        {
            return View();
        }

        [HttpPost]
        public IActionResult BookAppointment(string specialization, string doctorName, DateTime appointmentDate, string reason)
        {
            TempData["SuccessMessage"] = "Appointment request submitted successfully.";
            return RedirectToAction("History");
        }

        public IActionResult History()
        {
            return View();
        }
    }
}