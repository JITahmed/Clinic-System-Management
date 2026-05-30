using ClinicSystem.Api.Data;
using ClinicSystem.Api.Models;
using ClinicSystem.Api.Services;
using ClinicSystem.web.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ClinicSystem.web.Controllers
{
    [Authorize(Roles = "Receptionist")]
    public class ReceptionistController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotificationService _notificationService;
        private readonly IHubContext<AppointmentHub> _hubContext;

        public ReceptionistController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            NotificationService notificationService,
            IHubContext<AppointmentHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.Today;

            var todayAppointments = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.AppointmentDate.Date == today)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            ViewBag.TotalToday = todayAppointments.Count;
            ViewBag.Waiting = todayAppointments.Count(a => a.Status == AppointmentStatus.Confirmed);
            ViewBag.InProgress = todayAppointments.Count(a => a.Status == AppointmentStatus.InProgress);
            ViewBag.Completed = todayAppointments.Count(a => a.Status == AppointmentStatus.Completed);
            ViewBag.Requested = todayAppointments.Count(a => a.Status == AppointmentStatus.Requested);

            return View(todayAppointments);
        }

        // patient search function
        public IActionResult PatientSearch()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PatientSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ViewBag.Results = new List<Patient>();
                return View();
            }

            var results = await _context.Patients
                .Include(p => p.User)
                .Where(p =>
                    p.CPRNumber.Contains(query) ||
                    p.PatientReferenceNumber.Contains(query) ||
                    p.User.FullName.Contains(query) ||
                    p.User.PhoneNumber!.Contains(query))
                .ToListAsync();

            ViewBag.Results = results;
            ViewBag.Query = query;
            return View();
        }

        // appointment listing function
        public async Task<IActionResult> AppointmentList(
            DateTime? date,
            AppointmentStatus? status,
            string? search)
        {
            var query = _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .AsQueryable();

            if (date.HasValue)
                query = query.Where(a => a.AppointmentDate.Date == date.Value.Date);
            else
                query = query.Where(a => a.AppointmentDate.Date == DateTime.Today);

            if (status.HasValue)
                query = query.Where(a => a.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(a =>
                    a.Patient.User.FullName.Contains(search) ||
                    a.Patient.CPRNumber.Contains(search) ||
                    a.AppointmentReferenceNumber.Contains(search));

            var appointments = await query.OrderBy(a => a.StartTime).ToListAsync();

            ViewBag.SelectedDate = date ?? DateTime.Today;
            ViewBag.SelectedStatus = status;
            ViewBag.Search = search;

            return View(appointments);
        }

        // booking an appointment for a patient
        public async Task<IActionResult> BookAppointment(int? patientId)
        {
            ViewBag.Patients = await _context.Patients
                .Include(p => p.User)
                .OrderBy(p => p.User.FullName)
                .ToListAsync();

            ViewBag.Specializations = await _context.Specializations.ToListAsync();
            ViewBag.SelectedPatientId = patientId;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDoctorsBySpecialization(int specializationId)
        {
            var doctors = await _context.DoctorSpecializations
                .Include(ds => ds.Doctor).ThenInclude(d => d.User)
                .Where(ds => ds.SpecializationId == specializationId && ds.Doctor.IsAvailable)
                .Select(ds => new { ds.Doctor.Id, Name = ds.Doctor.User.FullName })
                .ToListAsync();

            return Json(doctors);
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableSlots(int doctorId, DateTime date)
        {
            // Get doctor schedule for the whole day
            var dayOfWeek = date.DayOfWeek;
            var schedule = await _context.DoctorSchedules
                .FirstOrDefaultAsync(s => s.DoctorId == doctorId && s.DayOfWeek == dayOfWeek && s.IsActive);
                
            // if no schedule
            if (schedule == null)
            {
                TempData["Info"] = "The doctor hasn't set their schedule for the day yet. Please check back later or choose another day";
                return Json(new List<object>());
            }

            // check that the doctor is not on leave
            var onLeave = await _context.DoctorLeaves
                .AnyAsync(l => l.DoctorId == doctorId &&
                               l.StartDate.Date <= date.Date &&
                               l.EndDate.Date >= date.Date);
                               
            // if doctor on leave then no slots available
            if (onLeave)
            {
                TempData["Info"] = "The doctor is currently on leave for the selected date. Please pick another day or doctor";
                return Json(new List<object>());
            }

            // 30m slot within schedule window
            var slots = new List<object>();
            var current = schedule.StartTime;

            while (current.Add(TimeSpan.FromMinutes(30)) <= schedule.EndTime)
            {
                var slotEnd = current.Add(TimeSpan.FromMinutes(30));

                // check if slot is already booked
                var isBooked = await _context.Appointments
                    .AnyAsync(a => a.DoctorId == doctorId &&
                                   a.AppointmentDate.Date == date.Date &&
                                   a.StartTime == current &&
                                   a.Status != AppointmentStatus.Cancelled &&
                                   a.Status != AppointmentStatus.Missed);
                // if their is a free slot
                if (!isBooked)
                {
                    slots.Add(new
                    {
                        start = current.ToString(@"hh\:mm"),
                        end = slotEnd.ToString(@"hh\:mm"),
                        display = $"{current:hh\\:mm} – {slotEnd:hh\\:mm}"
                    });
                }

                current = slotEnd;
            }

            return Json(slots);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAppointment(
            int PatientId,
            int DoctorId,
            DateTime AppointmentDate,
            string StartTime,
            string ReasonForVisit)
        {
            // validate inputs
            if (!TimeSpan.TryParse(StartTime, out var start))
            {
                TempData["Error"] = "Invalid time slot selected";
                return RedirectToAction(nameof(BookAppointment));
            }

            var end = start.Add(TimeSpan.FromMinutes(30));

            var conflict = await _context.Appointments.AnyAsync(a =>
                a.DoctorId == DoctorId &&
                a.AppointmentDate.Date == AppointmentDate.Date &&
                a.StartTime == start &&
                a.Status != AppointmentStatus.Cancelled &&
                a.Status != AppointmentStatus.Missed);

            if (conflict)
            {
                TempData["Error"] = "Please pick another one or refresh to see the latest availability";
                return RedirectToAction(nameof(BookAppointment), new { patientId = PatientId });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var refNumber = $"APT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

            var appointment = new Appointment
            {
                PatientId = PatientId,
                DoctorId = DoctorId,
                AppointmentDate = AppointmentDate,
                StartTime = start,
                EndTime = end,
                ReasonForVisit = ReasonForVisit,
                Status = AppointmentStatus.Confirmed,
                AppointmentReferenceNumber = refNumber,
                CreatedByUserId = currentUser!.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            // send notifications
            var patient = await _context.Patients.Include(p => p.User).FirstAsync(p => p.Id == PatientId);
            var doctor = await _context.Doctors.Include(d => d.User).FirstAsync(d => d.Id == DoctorId);

            await _notificationService.AppointmentConfirmedAsync(
                patient.UserId,
                doctor.UserId,
                doctor.User.FullName,
                AppointmentDate,
                appointment.Id);

            // broadcast to SignalR live board
            await _hubContext.Clients.All.SendAsync("AppointmentUpdated", new
            {
                appointment.Id,
                appointment.AppointmentReferenceNumber,
                PatientName = patient.User.FullName,
                DoctorName = doctor.User.FullName,
                appointment.AppointmentDate,
                StartTime = appointment.StartTime.ToString(@"hh\:mm"),
                Status = appointment.Status.ToString()
            });

            TempData["Success"] = $"Appointment booked successfully. Reference: {refNumber}";
            return RedirectToAction(nameof(AppointmentList));
        }

        // appointment details
        public async Task<IActionResult> AppointmentDetails(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                    .ThenInclude(u => u.Doctor)
                        .ThenInclude(d => d!.DoctorSpecializations)
                            .ThenInclude(ds => ds.Specialization)
                .Include(a => a.VisitRecord)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();
            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int appointmentId, AppointmentStatus newStatus, string? cancellationReason)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null) return NotFound();

            if (!IsValidTransition(appointment.Status, newStatus))
            {
                TempData["Error"] = $"Cannot move from {appointment.Status} to {newStatus}.";
                return RedirectToAction(nameof(AppointmentDetails), new { id = appointmentId });
            }

            if (newStatus == AppointmentStatus.Cancelled)
            {
                if (string.IsNullOrWhiteSpace(cancellationReason))
                {
                    TempData["Error"] = "A cancellation reason is required.";
                    return RedirectToAction(nameof(AppointmentDetails), new { id = appointmentId });
                }
                appointment.CancellationReason = cancellationReason;
            }

            var previous = appointment.Status;
            appointment.Status = newStatus;
            await _context.SaveChangesAsync();

            await SendStatusNotificationAsync(appointment, previous, newStatus);

            // broadcast live update to SignalR board
            await _hubContext.Clients.All.SendAsync("AppointmentUpdated", new
            {
                appointment.Id,
                appointment.AppointmentReferenceNumber,
                PatientName = appointment.Patient.User.FullName,
                DoctorName = appointment.Doctor.User.FullName,
                appointment.AppointmentDate,
                StartTime = appointment.StartTime.ToString(@"hh\:mm"),
                Status = appointment.Status.ToString()
            });

            TempData["Success"] = $"Appointment status updated to {newStatus}.";
            return RedirectToAction(nameof(AppointmentDetails), new { id = appointmentId });
        }

        // cancelling appointment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelAppointment(int appointmentId, string cancellationReason)
        {
            return await UpdateStatus(appointmentId, AppointmentStatus.Cancelled, cancellationReason);
        }

        // The live board
        public async Task<IActionResult> LiveBoard()
        {
            var today = DateTime.Today;
            var appointments = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.AppointmentDate.Date == today &&
                            a.Status != AppointmentStatus.Cancelled &&
                            a.Status != AppointmentStatus.Missed)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            return View(appointments);
        }

        // helper functions
        private static bool IsValidTransition(AppointmentStatus current, AppointmentStatus next)
        {
            return (current, next) switch
            {
                (AppointmentStatus.Requested, AppointmentStatus.Confirmed) => true,
                (AppointmentStatus.Requested, AppointmentStatus.Cancelled) => true,
                (AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn) => true,
                (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled) => true,
                (AppointmentStatus.Confirmed, AppointmentStatus.Missed) => true,
                (AppointmentStatus.CheckedIn, AppointmentStatus.InProgress) => true,
                (AppointmentStatus.CheckedIn, AppointmentStatus.Cancelled) => true,
                (AppointmentStatus.InProgress, AppointmentStatus.Completed) => true,
                _ => false
            };
        }

        private async Task SendStatusNotificationAsync(Appointment appointment, AppointmentStatus previous, AppointmentStatus next)
        {
            var patient = appointment.Patient;
            var doctor = appointment.Doctor;

            switch (next)
            {
                case AppointmentStatus.Confirmed:
                    await _notificationService.AppointmentConfirmedAsync(
                        patient.UserId, doctor.UserId,
                        doctor.User.FullName, appointment.AppointmentDate, appointment.Id);
                    break;

                case AppointmentStatus.CheckedIn:
                    await _notificationService.AppointmentCheckedInAsync(
                        doctor.UserId, patient.User.FullName,
                        appointment.AppointmentDate, appointment.Id);
                    break;

                case AppointmentStatus.Completed:
                    await _notificationService.AppointmentCompletedAsync(
                        patient.UserId, doctor.User.FullName,
                        appointment.AppointmentDate, appointment.Id);
                    break;

                case AppointmentStatus.Cancelled:
                    await _notificationService.AppointmentCancelledAsync(
                        patient.UserId, doctor.UserId,
                        patient.User.FullName, doctor.User.FullName,
                        appointment.AppointmentDate, appointment.Id);
                    break;

                case AppointmentStatus.Missed:
                    await _notificationService.AppointmentMissedAsync(
                        patient.UserId, doctor.User.FullName,
                        appointment.AppointmentDate, appointment.Id);
                    break;
            }
        }
    }
}
