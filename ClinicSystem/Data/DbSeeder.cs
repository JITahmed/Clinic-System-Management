using ClinicSystem.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace ClinicSystem.Api.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            // Step 1: Create the 4 roles if they don't exist
            string[] roles = { "ClinicManager", "Doctor", "Receptionist", "Patient" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Step 2: Create test accounts — one per role
            await CreateUserIfNotExists(userManager,
                email: "manager@clinic.com",
                fullName: "Sarah Manager",
                password: "Admin123!",
                role: "ClinicManager");

            await CreateUserIfNotExists(userManager,
                email: "doctor@clinic.com",
                fullName: "Dr. Ahmed Hassan",
                password: "Admin123!",
                role: "Doctor");

            await CreateUserIfNotExists(userManager,
                email: "receptionist@clinic.com",
                fullName: "Fatima Reception",
                password: "Admin123!",
                role: "Receptionist");

            await CreateUserIfNotExists(userManager,
                email: "patient@clinic.com",
                fullName: "Ali Patient",
                password: "Admin123!",
                role: "Patient");
        }

        private static async Task CreateUserIfNotExists(
            UserManager<ApplicationUser> userManager,
            string email,
            string fullName,
            string password,
            string role)
        {
            // Only create if the user doesn't already exist
            if (await userManager.FindByEmailAsync(email) == null)
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(user, password);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }
    }
}