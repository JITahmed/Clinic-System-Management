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

        public async Task<IActionResult> Dashboard()
        {
            string? userId = _userManager.GetUserId(User);

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
            {
                return NotFound("Patient profile not found.");
            }

            DateTime today = DateTime.Today;
            TimeSpan currentTime = DateTime.Now.TimeOfDay;

            int upcomingCount = await _context.Appointments
                .CountAsync(a => a.PatientId == patient.Id
                    &&
                    (
                        a.AppointmentDate > today ||
                        (a.AppointmentDate == today && a.StartTime >= currentTime)
                    )
                    &&
                    (
                        a.Status == AppointmentStatus.Requested ||
                        a.Status == AppointmentStatus.Confirmed
                    ));

            int totalVisits = await _context.Appointments
                .CountAsync(a => a.PatientId == patient.Id && a.VisitRecord != null);

            int unreadNotifications = await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            var latestNotification = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .FirstOrDefaultAsync();

            ViewBag.UpcomingCount = upcomingCount;
            ViewBag.TotalVisits = totalVisits;
            ViewBag.UnreadNotifications = unreadNotifications;
            ViewBag.LatestNotificationTitle = latestNotification?.Title ?? "No recent notifications";

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

        [HttpGet]
        public async Task<IActionResult> EditProfile()
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(
            string CPRNumber,
            DateTime DateOfBirth,
            string Gender,
            string BloodType,
            string Allergies,
            string Address,
            string EmergencyContactName,
            string EmergencyContactPhone,
            string PhoneNumber)
        {
            string? userId = _userManager.GetUserId(User);

            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
            {
                return NotFound("Patient profile not found.");
            }

            if (string.IsNullOrWhiteSpace(CPRNumber))
            {
                ModelState.AddModelError("CPRNumber", "CPR number is required.");
            }

            if (DateOfBirth == default)
            {
                ModelState.AddModelError("DateOfBirth", "Date of birth is required.");
            }

            if (string.IsNullOrWhiteSpace(Gender))
            {
                ModelState.AddModelError("Gender", "Gender is required.");
            }

            if (!ModelState.IsValid)
            {
                return View(patient);
            }

            patient.CPRNumber = CPRNumber;
            patient.DateOfBirth = DateOfBirth;
            patient.Gender = Gender;
            patient.BloodType = BloodType ?? string.Empty;
            patient.Allergies = Allergies ?? string.Empty;
            patient.Address = Address ?? string.Empty;
            patient.EmergencyContactName = EmergencyContactName ?? string.Empty;
            patient.EmergencyContactPhone = EmergencyContactPhone ?? string.Empty;

            if (patient.User != null)
            {
                patient.User.PhoneNumber = PhoneNumber ?? string.Empty;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Profile updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public async Task<IActionResult> BrowseDoctors(int? specializationId)
        {
            var doctorsQuery = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.DoctorSpecializations)
                    .ThenInclude(ds => ds.Specialization)
                .Where(d => d.IsAvailable)
                .AsQueryable();

            if (specializationId.HasValue && specializationId.Value > 0)
            {
                doctorsQuery = doctorsQuery.Where(d =>
                    d.DoctorSpecializations.Any(ds => ds.SpecializationId == specializationId.Value));
            }

            var doctors = await doctorsQuery
                .OrderBy(d => d.Id)
                .ToListAsync();

            ViewBag.Specializations = await _context.Specializations
                .OrderBy(s => s.Name)
                .ToListAsync();

            ViewBag.SelectedSpecializationId = specializationId ?? 0;

            return View(doctors);
        }

        [HttpGet]
        public async Task<IActionResult> BookAppointment(int? doctorId)
        {
            await LoadBookingDropdowns();

            if (doctorId.HasValue)
            {
                var doctor = await _context.Doctors
                    .Include(d => d.User)
                    .Include(d => d.DoctorSpecializations)
                    .FirstOrDefaultAsync(d => d.Id == doctorId.Value);

                if (doctor != null)
                {
                    ViewBag.PreselectedDoctorId = doctor.Id;
                    ViewBag.PreselectedDoctorName = doctor.User.FullName;
                    ViewBag.PreselectedSpecializationId = doctor.DoctorSpecializations
                        .FirstOrDefault()?.SpecializationId;
                }
            }

            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAppointment(
            int specializationId,
            int doctorId,
            DateTime appointmentDate,
            string reasonForVisit,
            string? otherReasonForVisit)
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

            if (string.IsNullOrWhiteSpace(reasonForVisit))
            {
                ModelState.AddModelError("", "Please select a reason for visit.");
                await LoadBookingDropdowns();
                return View();
            }

            string finalReasonForVisit = reasonForVisit;

            if (reasonForVisit == "Other")
            {
                if (string.IsNullOrWhiteSpace(otherReasonForVisit))
                {
                    ModelState.AddModelError("", "Please write the reason for your visit.");
                    await LoadBookingDropdowns();
                    return View();
                }

                finalReasonForVisit = otherReasonForVisit.Trim();
            }

            bool doctorMatchesSpecialization = await _context.DoctorSpecializations.AnyAsync(ds =>
                ds.DoctorId == doctorId &&
                ds.SpecializationId == specializationId);

            if (!doctorMatchesSpecialization)
            {
                ModelState.AddModelError("", "Please select a doctor that matches the selected specialization.");
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
                ReasonForVisit = finalReasonForVisit,
                CreatedByUserId = userId ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            await CreatePatientNotificationAsync(
                userId,
                "Appointment Request Sent",
                $"Your appointment request for {appointmentDay:dd MMM yyyy} at {startTime:hh\\:mm} has been submitted successfully.",
                NotificationType.AppointmentBooked,
                appointment.Id
            );

            var bookingDoctor = await _context.Doctors
                .FirstOrDefaultAsync(d => d.Id == doctorId);

            var bookingPatientName = await _context.Patients
                .Where(p => p.Id == patient.Id)
                .Select(p => p.User.FullName)
                .FirstOrDefaultAsync();

            await CreatePatientNotificationAsync(
                bookingDoctor?.UserId,
                "New Appointment Request",
                $"{bookingPatientName} has requested an appointment on {appointmentDay:dd MMM yyyy} at {startTime:hh\\:mm}.",
                NotificationType.AppointmentBooked,
                appointment.Id
            );

            TempData["SuccessMessage"] = "Appointment request submitted successfully.";
            return RedirectToAction(nameof(Upcoming));
        }

        public async Task<IActionResult> Upcoming()
        {
            string? userId = _userManager.GetUserId(User);

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
            {
                return NotFound("Patient profile not found.");
            }

            DateTime today = DateTime.Today;
            TimeSpan currentTime = DateTime.Now.TimeOfDay;

            var appointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Where(a => a.PatientId == patient.Id
                    &&
                    (
                        a.AppointmentDate > today ||
                        (a.AppointmentDate == today && a.StartTime >= currentTime)
                    )
                    &&
                    (
                        a.Status == AppointmentStatus.Requested ||
                        a.Status == AppointmentStatus.Confirmed
                    ))
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .ToListAsync();

            return View(appointments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelAppointment(int id)
        {
            string? userId = _userManager.GetUserId(User);

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
            {
                return NotFound("Patient profile not found.");
            }

            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a => a.Id == id && a.PatientId == patient.Id);

            if (appointment == null)
            {
                return NotFound("Appointment not found.");
            }

            if (appointment.Status != AppointmentStatus.Requested &&
                appointment.Status != AppointmentStatus.Confirmed)
            {
                TempData["ErrorMessage"] = "You can only cancel appointments that are requested or confirmed.";
                return RedirectToAction(nameof(Upcoming));
            }

            appointment.Status = AppointmentStatus.Cancelled;

            await _context.SaveChangesAsync();

            await CreatePatientNotificationAsync(
                userId,
                "Appointment Cancelled",
                $"Your appointment on {appointment.AppointmentDate:dd MMM yyyy} at {appointment.StartTime:hh\\:mm} has been cancelled.",
                NotificationType.AppointmentCancelled,
                appointment.Id
            );

            TempData["SuccessMessage"] = "Appointment cancelled successfully.";
            return RedirectToAction(nameof(Upcoming));
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
                .Include(a => a.VisitRecord)
                .Where(a => a.PatientId == patient.Id)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToListAsync();

            var visitRecordIds = appointments
                .Where(a => a.VisitRecord != null)
                .Select(a => a.VisitRecord!.Id)
                .ToList();

            ViewBag.Prescriptions = await _context.Prescriptions
                .Where(p => visitRecordIds.Contains(p.VisitRecordId))
                .ToListAsync();

            return View(appointments);
        }

        public async Task<IActionResult> VisitDetails(int id)
        {
            string? userId = _userManager.GetUserId(User);

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
            {
                return NotFound("Patient profile not found.");
            }

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.VisitRecord)
                .FirstOrDefaultAsync(a =>
                    a.Id == id &&
                    a.PatientId == patient.Id &&
                    a.VisitRecord != null);

            if (appointment == null)
            {
                return NotFound("Visit record not found.");
            }

            var prescriptions = await _context.Prescriptions
                .Where(p => p.VisitRecordId == appointment.VisitRecord!.Id)
                .ToListAsync();

            ViewBag.Prescriptions = prescriptions;

            return View(appointment);
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
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        private async Task CreatePatientNotificationAsync(
            string? userId,
            string title,
            string message,
            NotificationType type,
            int? relatedEntityId = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                IsRead = false,
                RelatedEntityId = relatedEntityId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationAsRead(int id)
        {
            var userId = _userManager.GetUserId(User);
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Notifications");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            var userId = _userManager.GetUserId(User);
            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
                n.IsRead = true;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "All notifications marked as read.";
            return RedirectToAction("Notifications");
        }

        public async Task<IActionResult> Notifications()
        {
            var userId = _userManager.GetUserId(User);
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
            return View(notifications);
        }








    }
}
