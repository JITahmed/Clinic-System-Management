using ClinicSystem.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;      
using ClinicSystem.Api.DTOs;
using ClinicSystem.Api.Models;

namespace ClinicSystem.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AppointmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("lookup")]
        [AllowAnonymous]
        public async Task<IActionResult> PublicLookup([FromQuery] string cpr, [FromQuery] string refNumber)
        {
            var patient = await _context.Patients
                .Include(p => p.Appointments)
                    .ThenInclude(a => a.Doctor)
                .Include(p => p.Appointments)
                    .ThenInclude(a => a.VisitRecord)
                .FirstOrDefaultAsync(p => p.CPRNumber == cpr && p.PatientReferenceNumber == refNumber);

            if (patient == null) return NotFound("Patient not found");

            var response = new PublicLookupResponse
            {
                 Upcoming = patient.Appointments
                .Where(a => a.AppointmentDate >= DateTime.Today &&
                            (a.Status == AppointmentStatus.Requested ||
                             a.Status == AppointmentStatus.Confirmed ||
                             a.Status == AppointmentStatus.CheckedIn))
                .OrderBy(a => a.AppointmentDate)
                .Select(a => new UpcomingAppointment
                {
                    Id = a.Id,
                    AppointmentDate = a.AppointmentDate,
                    Status = a.Status.ToString(),
                    DoctorName = a.Doctor.User.FullName,  // ← also fix this (was using UserId)
                }).ToList(),

                RecentVisits = patient.Appointments
                    .Where(a => a.Status == AppointmentStatus.Completed && a.VisitRecord != null)
                    .OrderByDescending(a => a.AppointmentDate)
                    .Take(3)
                    .Select(a => new RecentVisit
                    {
                        Id = a.Id,
                        AppointmentDate = a.AppointmentDate,
                        Diagnosis = a.VisitRecord.Diagnosis
                    }).ToList()
            };

            return Ok(response);
        }

        [HttpGet]
        [Authorize]  // Requires JWT
        public async Task<IActionResult> GetAllAppointments([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var query = _context.Appointments.Include(a => a.Doctor).Include(a => a.Patient).AsQueryable();
            if (from.HasValue) query = query.Where(a => a.AppointmentDate >= from.Value);
            if (to.HasValue) query = query.Where(a => a.AppointmentDate <= to.Value);

            return Ok(await query.ToListAsync());
        }
    }
}
