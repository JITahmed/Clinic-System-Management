using ClinicSystem.Api.Data;
using ClinicSystem.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicSystem.web.Controllers
{
    [Authorize(Roles = "Patient")]
    public class PatientController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PatientController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        public async Task<IActionResult> Profile()
        {
            string? userId = _userManager.GetUserId(User);

            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
            {
                return NotFound("Patient profile not found.");
            }

            return View(patient);
        }

        public async Task<IActionResult> BookAppointment()
        {
            await LoadBookingDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAppointment(int doctorId, DateTime appointmentDate, string reasonForVisit)
        {
            string? userId = _userManager.GetUserId(User);

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
            {
                return NotFound("Patient profile not found.");
            }

            if (appointmentDate <= DateTime.Now)
            {
                ModelState.AddModelError("", "Please choose a future appointment date and time.");
                await LoadBookingDropdowns();
                return View();
            }

            DateTime appointmentDay = appointmentDate.Date;
            TimeSpan startTime = appointmentDate.TimeOfDay;
            TimeSpan endTime = startTime.Add(TimeSpan.FromMinutes(30));

            bool isDoubleBooked = await _context.Appointments.AnyAsync(a =>
                a.DoctorId == doctorId &&
                a.AppointmentDate >= appointmentDay &&
                a.AppointmentDate < appointmentDay.AddDays(1) &&
                a.Status != AppointmentStatus.Cancelled &&
                a.Status != AppointmentStatus.Missed &&
                startTime < a.EndTime &&
                endTime > a.StartTime
            );

            if (isDoubleBooked)
            {
                ModelState.AddModelError("", "This doctor already has an appointment at this time. Please choose another time.");
                await LoadBookingDropdowns();
                return View();
            }

            var appointment = new Appointment
            {
                PatientId = patient.Id,
                DoctorId = doctorId,
                AppointmentReferenceNumber = $"APT-{DateTime.UtcNow:yyyyMMddHHmmss}",
                AppointmentDate = appointmentDay,
                StartTime = startTime,
                EndTime = endTime,
                Status = AppointmentStatus.Requested,
                ReasonForVisit = reasonForVisit,
                CreatedByUserId = userId ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Appointment request submitted successfully.";
            return RedirectToAction(nameof(History));
        }

        public async Task<IActionResult> History()
        {
            string? userId = _userManager.GetUserId(User);

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
            {
                return NotFound("Patient profile not found.");
            }

            var appointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Where(a => a.PatientId == patient.Id)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToListAsync();

            return View(appointments);
        }

        private async Task LoadBookingDropdowns()
        {
            ViewBag.Doctors = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.DoctorSpecializations)
                    .ThenInclude(ds => ds.Specialization)
                .Where(d => d.IsAvailable)
                .OrderBy(d => d.Id)
                .ToListAsync();

            ViewBag.Specializations = await _context.Specializations
                .OrderBy(s => s.Id)
                .ToListAsync();
        }
    }
}