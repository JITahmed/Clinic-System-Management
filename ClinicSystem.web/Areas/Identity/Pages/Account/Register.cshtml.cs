#nullable disable
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using ClinicSystem.Api.Models;
using ClinicSystem.Api.Data;

namespace ClinicSystem.web.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly ApplicationDbContext _context;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }
        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Full Name")]
            public string FullName { get; set; }

            [Display(Name = "CPR Number")]
            public string? CPRNumber { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            [Display(Name = "Register as")]
            public string Role { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = Input.Email,
                    Email = Input.Email,
                    FullName = Input.FullName,
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    // Use a transaction so if anything fails, everything rolls back
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var allowedRoles = new[] { "Patient", "Doctor" };
                        var role = allowedRoles.Contains(Input.Role) ? Input.Role : "Patient";
                        await _userManager.AddToRoleAsync(user, role);

                        if (role == "Patient")
                        {
                            var patient = new Patient
                            {
                                UserId = user.Id,
                                CPRNumber = !string.IsNullOrWhiteSpace(Input.CPRNumber)
                                ? Input.CPRNumber.Trim()
                                : "TEMP-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                                PatientReferenceNumber = "PAT-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                                Gender = string.Empty,
                                BloodType = string.Empty,
                                Allergies = string.Empty,
                                Address = string.Empty,
                                EmergencyContactName = string.Empty,
                                EmergencyContactPhone = string.Empty,
                                DateOfBirth = DateTime.UtcNow
                            };
                            _context.Patients.Add(patient);
                            await _context.SaveChangesAsync();
                        }

                        if (role == "Doctor")
                        {
                            var doctor = new Doctor
                            {
                                UserId = user.Id,
                                LicenseNumber = "LIC-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                                Bio = string.Empty,
                                YearsOfExperience = 0,
                                ConsultationFee = 0,
                                IsAvailable = true
                            };
                            _context.Doctors.Add(doctor);
                            await _context.SaveChangesAsync();
                        }

                        await transaction.CommitAsync();
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        await _userManager.DeleteAsync(user);
                        ModelState.AddModelError(string.Empty, "Registration failed. Please try again.");
                        return Page();
                    }
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return Page();
        }
    }
}