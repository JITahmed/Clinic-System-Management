using ClinicSystem.Api.Data;
using ClinicSystem.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicSystem.web.Controllers
{
    [Authorize(Roles = "Doctor")]
    public class DoctorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DoctorController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<Doctor?> GetCurrentDoctorAsync()
        {
            var userId = _userManager.GetUserId(User);
            return await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.DoctorSpecializations).ThenInclude(ds => ds.Specialization)
                .FirstOrDefaultAsync(d => d.UserId == userId);
        }

        public async Task<IActionResult> Dashboard()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound("Doctor profile not found.");

            var today = DateTime.Today;
            var currentTime = DateTime.Now.TimeOfDay;

            var appointments = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Where(a => a.DoctorId == doctor.Id && a.AppointmentDate.Date == today)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            int upcomingCount = await _context.Appointments
                .CountAsync(a => a.DoctorId == doctor.Id
                    && (a.AppointmentDate > today || (a.AppointmentDate == today && a.StartTime >= currentTime))
                    && (a.Status == AppointmentStatus.Requested || a.Status == AppointmentStatus.Confirmed));

            int completedToday = appointments.Count(a => a.Status == AppointmentStatus.Completed);
            int checkedIn = appointments.Count(a => a.Status == AppointmentStatus.CheckedIn);
            int confirmed = appointments.Count(a => a.Status == AppointmentStatus.Confirmed);

            int unreadCount = await _context.Notifications
                .CountAsync(n => n.UserId == doctor.UserId && !n.IsRead);

            var latestNotification = await _context.Notifications
                .Where(n => n.UserId == doctor.UserId)
                .OrderByDescending(n => n.CreatedAt)
                .FirstOrDefaultAsync();

            ViewBag.Doctor = doctor;
            ViewBag.TotalToday = appointments.Count;
            ViewBag.UpcomingCount = upcomingCount;
            ViewBag.CompletedToday = completedToday;
            ViewBag.CheckedIn = checkedIn;
            ViewBag.Confirmed = confirmed;
            ViewBag.UnreadCount = unreadCount;
            ViewBag.LatestNotificationTitle = latestNotification?.Title ?? "No recent notifications";

            return View(appointments);
        }

        public async Task<IActionResult> Consultation(int id)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.VisitRecord).ThenInclude(v => v!.Prescriptions)
                .FirstOrDefaultAsync(a => a.Id == id && a.DoctorId == doctor.Id);

            if (appointment == null) return NotFound();

            if (appointment.Status != AppointmentStatus.CheckedIn && appointment.Status != AppointmentStatus.InProgress)
            {
                TempData["ErrorMessage"] = "This appointment cannot be started.";
                return RedirectToAction(nameof(Dashboard));
            }

            if (appointment.Status == AppointmentStatus.CheckedIn)
            {
                appointment.Status = AppointmentStatus.InProgress;
                await _context.SaveChangesAsync();
            }

            var vm = new web.Models.ConsultationViewModel
            {
                AppointmentId = appointment.Id,
                PatientId = appointment.PatientId,
                PatientName = appointment.Patient.User.FullName,
                ReasonForVisit = appointment.ReasonForVisit,
                PatientDateOfBirth = appointment.Patient.DateOfBirth,
                BloodType = appointment.Patient.BloodType,
                Allergies = appointment.Patient.Allergies,
                Symptoms = appointment.VisitRecord?.DoctorNotes ?? string.Empty,
                DiagnosisText = appointment.VisitRecord?.Diagnosis ?? string.Empty,
                TreatmentPlan = appointment.VisitRecord?.TreatmentPlan ?? string.Empty,
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveConsultation(web.Models.ConsultationViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Consultation", model);

            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.VisitRecord).ThenInclude(v => v!.Prescriptions)
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a => a.Id == model.AppointmentId && a.DoctorId == doctor.Id);

            if (appointment == null) return NotFound();

            if (appointment.VisitRecord == null)
            {
                appointment.VisitRecord = new VisitRecord
                {
                    AppointmentId = appointment.Id,
                    VisitDate = DateTime.UtcNow
                };
                _context.VisitRecords.Add(appointment.VisitRecord);
                await _context.SaveChangesAsync();
            }

            appointment.VisitRecord.DoctorNotes = model.Symptoms;
            appointment.VisitRecord.Diagnosis = model.DiagnosisText;
            appointment.VisitRecord.TreatmentPlan = model.TreatmentPlan;

            if (!string.IsNullOrWhiteSpace(model.MedicationName))
            {
                _context.Prescriptions.Add(new Prescription
                {
                    VisitRecordId = appointment.VisitRecord.Id,
                    MedicationName = model.MedicationName,
                    Dosage = model.Dosage ?? string.Empty,
                    Frequency = model.Frequency ?? string.Empty,
                    Duration = model.Duration ?? string.Empty,
                    Instructions = model.Instructions ?? string.Empty
                });
            }

            appointment.Status = AppointmentStatus.Completed;

            _context.Notifications.Add(new Notification
            {
                UserId = appointment.Patient.UserId,
                Title = "Visit Completed",
                Message = $"Your appointment on {appointment.AppointmentDate:dd MMM yyyy} has been completed. Your visit notes are now available.",
                Type = NotificationType.AppointmentCompleted,
                RelatedEntityId = appointment.Id,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Consultation for {appointment.Patient.User.FullName} saved successfully.";
            return RedirectToAction(nameof(Dashboard));
        }

        public async Task<IActionResult> PatientHistory(int id)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (patient == null) return NotFound();

            var hadAppointment = await _context.Appointments
                .AnyAsync(a => a.PatientId == id && a.DoctorId == doctor.Id);

            if (!hadAppointment)
            {
                TempData["ErrorMessage"] = "You can only view history for your own patients.";
                return RedirectToAction(nameof(Dashboard));
            }

            var visitRecords = await _context.VisitRecords
                .Include(v => v.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
                .Include(v => v.Prescriptions)
                .Where(v => v.Appointment.PatientId == id)
                .OrderByDescending(v => v.VisitDate)
                .ToListAsync();

            ViewBag.Patient = patient;
            return View(visitRecords);
        }

        public async Task<IActionResult> Prescriptions(int id)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var visitRecord = await _context.VisitRecords
                .Include(v => v.Prescriptions)
                .Include(v => v.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(v => v.Id == id && v.Appointment.DoctorId == doctor.Id);

            if (visitRecord == null) return NotFound();

            return View(visitRecord);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPrescription(int visitRecordId, string medicationName,
            string dosage, string frequency, string duration, string instructions)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var visitRecord = await _context.VisitRecords
                .Include(v => v.Appointment)
                .FirstOrDefaultAsync(v => v.Id == visitRecordId && v.Appointment.DoctorId == doctor.Id);

            if (visitRecord == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(medicationName))
            {
                _context.Prescriptions.Add(new Prescription
                {
                    VisitRecordId = visitRecordId,
                    MedicationName = medicationName,
                    Dosage = dosage ?? string.Empty,
                    Frequency = frequency ?? string.Empty,
                    Duration = duration ?? string.Empty,
                    Instructions = instructions ?? string.Empty
                });
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Prescription added.";
            }

            return RedirectToAction(nameof(Prescriptions), new { id = visitRecordId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePrescription(int prescriptionId, int visitRecordId)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var prescription = await _context.Prescriptions
                .Include(p => p.VisitRecord).ThenInclude(v => v.Appointment)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId && p.VisitRecord.Appointment.DoctorId == doctor.Id);

            if (prescription != null)
            {
                _context.Prescriptions.Remove(prescription);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Prescription removed.";
            }

            return RedirectToAction(nameof(Prescriptions), new { id = visitRecordId });
        }

        public async Task<IActionResult> Notifications()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == doctor.UserId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            foreach (var n in notifications.Where(n => !n.IsRead))
                n.IsRead = true;

            await _context.SaveChangesAsync();
            return View(notifications);
        }

        public async Task<IActionResult> MySchedule()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var schedules = await _context.DoctorSchedules
                .Where(s => s.DoctorId == doctor.Id && s.IsActive)
                .OrderBy(s => s.DayOfWeek)
                .ToListAsync();

            var leaves = await _context.DoctorLeaves
                .Where(l => l.DoctorId == doctor.Id && l.EndDate >= DateTime.Today)
                .OrderBy(l => l.StartDate)
                .ToListAsync();

            ViewBag.Doctor = doctor;
            ViewBag.Leaves = leaves;
            return View(schedules);
        }

        public async Task<IActionResult> UpcomingAppointments()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var upcoming = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Where(a => a.DoctorId == doctor.Id
                    && (a.AppointmentDate > DateTime.Today
                        || (a.AppointmentDate == DateTime.Today && a.StartTime >= DateTime.Now.TimeOfDay))
                    && (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Requested))
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .ToListAsync();

            return View(upcoming);
        }
    }
}