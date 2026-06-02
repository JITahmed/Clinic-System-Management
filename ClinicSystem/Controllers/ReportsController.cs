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
    [Authorize(Roles = "ClinicManager")]
    public class ReportsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetAppointmentStats()
        {
            var stats = await _context.Appointments
                .GroupBy(a => a.Status)
                .Select(g => new StatusCount { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();
            return Ok(stats);
        }

        [HttpGet("cancellations")]
        public async Task<IActionResult> GetCancellationRates()
        {
            int total = await _context.Appointments.CountAsync();
            int cancelled = await _context.Appointments.CountAsync(a => a.Status == AppointmentStatus.Cancelled);
            int missed = await _context.Appointments.CountAsync(a => a.Status == AppointmentStatus.Missed);

            var result = new CancellationData
            {
                CancellationRate = total == 0 ? 0 : (double)cancelled / total,
                MissedRate = total == 0 ? 0 : (double)missed / total
            };
            return Ok(result);
        }
    }
}
