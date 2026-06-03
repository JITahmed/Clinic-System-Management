using ClinicSystem.Api.Data;
using ClinicSystem.Api.Models;
using ClinicSystem.web.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ClinicSystem.web.Controllers
{
    [Authorize(Roles = "Doctor")]
    public class DoctorController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ClinicSystem.Api.Services.NotificationService _notificationService;
        private readonly IHubContext<AppointmentHub> _hubContext;

        public DoctorController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ClinicSystem.Api.Services.NotificationService notificationService,
            IHubContext<AppointmentHub> hubContext)
        {
            _db = db;
            _userManager = userManager;
            _notificationService = notificationService;
            _hubContext = hubContext;

        }

        private async Task<Doctor?> GetCurrentDoctorAsync()
        {
            var userId = _userManager.GetUserId(User);
            return await _db.Doctors
                .Include(d => d.User)
                .Include(d => d.DoctorSpecializations).ThenInclude(ds => ds.Specialization)
                .FirstOrDefaultAsync(d => d.UserId == userId);
        }

        public async Task<IActionResult> Dashboard()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound("Doctor profile not found.");

            var today = DateTime.Today;
            var appointments = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Where(a => a.DoctorId == doctor.Id
                    && a.AppointmentDate.Date == today
                    && a.Status != AppointmentStatus.Cancelled
                    && a.Status != AppointmentStatus.Missed)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            ViewBag.Doctor = doctor;
            ViewBag.UnreadCount = await _db.Notifications
                .CountAsync(n => n.UserId == doctor.UserId && !n.IsRead);

            return View(appointments);
        }


        public async Task<IActionResult> Consultation(int id)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var appointment = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.VisitRecord).ThenInclude(v => v!.Prescriptions)
                .FirstOrDefaultAsync(a => a.Id == id && a.DoctorId == doctor.Id);

            if (appointment == null) return NotFound();

            if (appointment.Status != AppointmentStatus.CheckedIn &&
                appointment.Status != AppointmentStatus.InProgress)
            {
                TempData["Error"] = "This appointment cannot be started.";
                return RedirectToAction(nameof(Dashboard));
            }

            if (appointment.Status == AppointmentStatus.CheckedIn)
            {
                appointment.Status = AppointmentStatus.InProgress;
                await _db.SaveChangesAsync();
                await BroadcastAppointmentAsync(appointment, appointment.Patient.User.FullName, doctor.User.FullName);
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

            var appointment = await _db.Appointments
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
                _db.VisitRecords.Add(appointment.VisitRecord);
                await _db.SaveChangesAsync();
            }

            appointment.VisitRecord.DoctorNotes = model.Symptoms;
            appointment.VisitRecord.Diagnosis = model.DiagnosisText;
            appointment.VisitRecord.TreatmentPlan = model.TreatmentPlan;

            if (!string.IsNullOrWhiteSpace(model.MedicationName))
            {
                var prescription = new Prescription
                {
                    VisitRecordId = appointment.VisitRecord.Id,
                    MedicationName = model.MedicationName,
                    Dosage = model.Dosage ?? string.Empty,
                    Frequency = model.Frequency ?? string.Empty,
                    Duration = model.Duration ?? string.Empty,
                    Instructions = model.Instructions ?? string.Empty
                };
                _db.Prescriptions.Add(prescription);
            }

            appointment.Status = AppointmentStatus.Completed;

            await _notificationService.AppointmentCompletedAsync(
                appointment.Patient.UserId,
                doctor.User.FullName,
                appointment.AppointmentDate,
                appointment.Id);

            await _db.SaveChangesAsync();

            await BroadcastAppointmentAsync(appointment, appointment.Patient.User.FullName, doctor.User.FullName);

            TempData["Success"] = $"Consultation for {appointment.Patient.User.FullName} saved.";
            return RedirectToAction(nameof(Dashboard));
        }

        public async Task<IActionResult> PatientHistory(int id)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var patient = await _db.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (patient == null) return NotFound();

            var hadAppointment = await _db.Appointments
                .AnyAsync(a => a.PatientId == id && a.DoctorId == doctor.Id);

            if (!hadAppointment)
            {
                TempData["Error"] = "You can only view history for your own patients.";
                return RedirectToAction(nameof(Dashboard));
            }

            var visitRecords = await _db.VisitRecords
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

            var visitRecord = await _db.VisitRecords
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

            var visitRecord = await _db.VisitRecords
                .Include(v => v.Appointment)
                .FirstOrDefaultAsync(v => v.Id == visitRecordId && v.Appointment.DoctorId == doctor.Id);

            if (visitRecord == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(medicationName))
            {
                _db.Prescriptions.Add(new Prescription
                {
                    VisitRecordId = visitRecordId,
                    MedicationName = medicationName,
                    Dosage = dosage ?? string.Empty,
                    Frequency = frequency ?? string.Empty,
                    Duration = duration ?? string.Empty,
                    Instructions = instructions ?? string.Empty
                });
                await _db.SaveChangesAsync();
                TempData["Success"] = "Prescription added.";
            }

            return RedirectToAction(nameof(Prescriptions), new { id = visitRecordId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePrescription(int prescriptionId, int visitRecordId)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var prescription = await _db.Prescriptions
                .Include(p => p.VisitRecord).ThenInclude(v => v.Appointment)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId
                    && p.VisitRecord.Appointment.DoctorId == doctor.Id);

            if (prescription != null)
            {
                _db.Prescriptions.Remove(prescription);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Prescription removed.";
            }

            return RedirectToAction(nameof(Prescriptions), new { id = visitRecordId });
        }


        public async Task<IActionResult> Notifications()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var notifications = await _db.Notifications
                .Where(n => n.UserId == doctor.UserId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            foreach (var n in notifications.Where(n => !n.IsRead))
                n.IsRead = true;

            await _db.SaveChangesAsync();
            return View(notifications);
        }

        public async Task<IActionResult> MySchedule()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var schedules = await _db.DoctorSchedules
                .Where(s => s.DoctorId == doctor.Id && s.IsActive)
                .OrderBy(s => s.DayOfWeek)
                .ToListAsync();

            var leaves = await _db.DoctorLeaves
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

            var upcoming = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Where(a => a.DoctorId == doctor.Id
                    && a.AppointmentDate.Date >= DateTime.Today
                    && (a.Status == AppointmentStatus.Confirmed
                        || a.Status == AppointmentStatus.Requested))
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .ToListAsync();

            ViewBag.Doctor = doctor;
            return View(upcoming);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmAppointment(int id)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return NotFound();

            var appointment = await _db.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a => a.Id == id && a.DoctorId == doctor.Id);

            if (appointment == null) return NotFound();

            if (appointment.Status != AppointmentStatus.Requested)
            {
                TempData["Error"] = "Only requested appointments can be confirmed.";
                return RedirectToAction(nameof(UpcomingAppointments));
            }

            appointment.Status = AppointmentStatus.Confirmed;
            await _db.SaveChangesAsync();

            await _notificationService.AppointmentConfirmedAsync(
                appointment.Patient.UserId,
                doctor.UserId,
                doctor.User.FullName,
                appointment.AppointmentDate,
                appointment.Id);

            await BroadcastAppointmentAsync(appointment, appointment.Patient.User.FullName, doctor.User.FullName);

            TempData["Success"] = $"Appointment with {appointment.Patient.User.FullName} confirmed.";
            return RedirectToAction(nameof(UpcomingAppointments));
        }

        private async Task BroadcastAppointmentAsync(Appointment appointment, string patientName, string doctorName)
        {
            await _hubContext.Clients.All.SendAsync("AppointmentUpdated", new
            {
                appointment.Id,
                appointment.AppointmentReferenceNumber,
                PatientName = patientName,
                DoctorName = doctorName,
                appointment.AppointmentDate,
                StartTime = appointment.StartTime.ToString(@"hh\:mm"),
                Status = appointment.Status.ToString()
            });
        }
    }
}
