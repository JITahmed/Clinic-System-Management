using ClinicSystem.Api.Data;
using ClinicSystem.Api.DTOs;
using ClinicSystem.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicSystem.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]  // All endpoints require JWT
    public class DoctorsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public DoctorsController(ApplicationDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAllDoctors()
        {
            var doctors = await _context.Doctors
                .Include(d => d.User)  // ← Get ApplicationUser for Name & Email
                .Include(d => d.DoctorSpecializations)
                    .ThenInclude(ds => ds.Specialization)  // ← Get Specialization details
                .Select(d => new
                {
                    d.Id,
                    Name = d.User != null ? d.User.UserName : "Unknown",      // or d.User.FullName
                    Email = d.User != null ? d.User.Email : "unknown@example.com",
                    Specializations = d.DoctorSpecializations
                        .Select(ds => ds.Specialization.Name)
                        .ToList()
                })
                .ToListAsync();

            return Ok(doctors);
        }
    }
}