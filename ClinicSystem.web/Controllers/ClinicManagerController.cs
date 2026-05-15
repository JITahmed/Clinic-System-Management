using ClinicSystem.Api.Data;
using ClinicSystem.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicSystem.web.Controllers
{
    [Authorize(Roles = "ClinicManager")]
    public class ClinicManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClinicManagerController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /ClinicManager/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var totalDoctors = await _context.Doctors.CountAsync();
            var totalPatients = await _context.Patients.CountAsync();
            var totalAppointments = await _context.Appointments.CountAsync();
            var pendingAppointments = await _context.Appointments
                .Where(a => a.Status == AppointmentStatus.Requested)
                .CountAsync();
            var todayAppointments = await _context.Appointments
                .Where(a => a.AppointmentDate.Date == DateTime.Today)
                .CountAsync();

            ViewBag.TotalDoctors = totalDoctors;
            ViewBag.TotalPatients = totalPatients;
            ViewBag.TotalAppointments = totalAppointments;
            ViewBag.PendingAppointments = pendingAppointments;
            ViewBag.TodayAppointments = todayAppointments;

            return View();
        }

        // ---- SPECIALIZATIONS -------------------------------------

        // GET: /ClinicManager/SpecializationList
        public async Task<IActionResult> SpecializationList()
        {
            var specializations = await _context.Specializations.ToListAsync();
            return View(specializations);
        }

        // GET: /ClinicManager/CreateSpecialization
        public IActionResult CreateSpecialization()
        {
            return View();
        }

        // POST: /ClinicManager/CreateSpecialization
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSpecialization(Specialization specialization)
        {
            if (ModelState.IsValid)
            {
                _context.Specializations.Add(specialization);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Specialization created successfully.";
                return RedirectToAction(nameof(SpecializationList));
            }
            return View(specialization);
        }

        // GET: /ClinicManager/EditSpecialization/5
        public async Task<IActionResult> EditSpecialization(int id)
        {
            var specialization = await _context.Specializations.FindAsync(id);
            if (specialization == null) return NotFound();
            return View(specialization);
        }

        // POST: /ClinicManager/EditSpecialization/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSpecialization(int id, Specialization specialization)
        {
            if (id != specialization.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Specializations.Update(specialization);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Specialization updated successfully.";
                return RedirectToAction(nameof(SpecializationList));
            }
            return View(specialization);
        }

        // POST: /ClinicManager/DeleteSpecialization/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSpecialization(int id)
        {
            var specialization = await _context.Specializations.FindAsync(id);
            if (specialization != null)
            {
                _context.Specializations.Remove(specialization);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Specialization deleted.";
            }
            return RedirectToAction(nameof(SpecializationList));
        }

        // ---- DOCTORS ------------------------------------

        // GET: /ClinicManager/DoctorList
        public async Task<IActionResult> DoctorList()
        {
            var doctors = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.DoctorSpecializations)
                    .ThenInclude(ds => ds.Specialization)
                .ToListAsync();
            return View(doctors);
        }

        // GET: /ClinicManager/CreateDoctor
        public async Task<IActionResult> CreateDoctor()
        {
            ViewBag.Specializations = await _context.Specializations.ToListAsync();
            return View();
        }

        // POST: /ClinicManager/CreateDoctor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDoctor(
            string FullName,
            string Email,
            string Password,
            string LicenseNumber,
            string Bio,
            int YearsOfExperience,
            decimal ConsultationFee,
            List<int> SelectedSpecializations)
        {
            // Create the login account
            var user = new ApplicationUser
            {
                UserName = Email,
                Email = Email,
                FullName = FullName,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Doctor");

                // Create the doctor profile
                var doctor = new Doctor
                {
                    UserId = user.Id,
                    LicenseNumber = LicenseNumber,
                    Bio = Bio,
                    YearsOfExperience = YearsOfExperience,
                    ConsultationFee = ConsultationFee,
                    IsAvailable = true
                };

                _context.Doctors.Add(doctor);
                await _context.SaveChangesAsync();

                // Assign specializations
                if (SelectedSpecializations != null)
                {
                    foreach (var specId in SelectedSpecializations)
                    {
                        _context.DoctorSpecializations.Add(new DoctorSpecialization
                        {
                            DoctorId = doctor.Id,
                            SpecializationId = specId
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Doctor created successfully.";
                return RedirectToAction(nameof(DoctorList));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.Specializations = await _context.Specializations.ToListAsync();
            return View();
        }

        // GET: /ClinicManager/DoctorDetails/5
        public async Task<IActionResult> DoctorDetails(int id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.DoctorSpecializations)
                    .ThenInclude(ds => ds.Specialization)
                .Include(d => d.Schedules)
                .Include(d => d.Leaves)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doctor == null) return NotFound();
            return View(doctor);
        }

        // GET: /ClinicManager/EditDoctor/5
        public async Task<IActionResult> EditDoctor(int id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.DoctorSpecializations)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doctor == null) return NotFound();

            ViewBag.Specializations = await _context.Specializations.ToListAsync();
            ViewBag.SelectedSpecializations = doctor.DoctorSpecializations
                .Select(ds => ds.SpecializationId).ToList();
            return View(doctor);
        }

        // POST: /ClinicManager/EditDoctor/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDoctor(
            int id,
            string FullName,
            string LicenseNumber,
            string Bio,
            int YearsOfExperience,
            decimal ConsultationFee,
            bool IsAvailable,
            List<int> SelectedSpecializations)
        {
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.DoctorSpecializations)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doctor == null) return NotFound();

            // Update user full name
            doctor.User.FullName = FullName;
            await _userManager.UpdateAsync(doctor.User);

            // Update doctor profile
            doctor.LicenseNumber = LicenseNumber;
            doctor.Bio = Bio;
            doctor.YearsOfExperience = YearsOfExperience;
            doctor.ConsultationFee = ConsultationFee;
            doctor.IsAvailable = IsAvailable;

            // Update specializations
            _context.DoctorSpecializations.RemoveRange(doctor.DoctorSpecializations);
            if (SelectedSpecializations != null)
            {
                foreach (var specId in SelectedSpecializations)
                {
                    _context.DoctorSpecializations.Add(new DoctorSpecialization
                    {
                        DoctorId = doctor.Id,
                        SpecializationId = specId
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Doctor updated successfully.";
            return RedirectToAction(nameof(DoctorList));
        }

        // POST: /ClinicManager/DeleteDoctor/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDoctor(int id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.DoctorSpecializations)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doctor != null)
            {
                _context.DoctorSpecializations.RemoveRange(doctor.DoctorSpecializations);
                _context.Doctors.Remove(doctor);
                await _userManager.DeleteAsync(doctor.User);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Doctor deleted successfully.";
            }
            return RedirectToAction(nameof(DoctorList));
        }





    }
}