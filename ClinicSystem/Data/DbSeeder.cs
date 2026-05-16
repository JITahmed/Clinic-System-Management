using ClinicSystem.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClinicSystem.Api.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
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
            var managerUser = await CreateUserIfNotExists(userManager,
                email: "manager@clinic.com",
                fullName: "Sarah Manager",
                password: "Admin123!",
                role: "ClinicManager");

            var doctorUser = await CreateUserIfNotExists(userManager,
                email: "doctor@clinic.com",
                fullName: "Dr. Ahmed Hassan",
                password: "Admin123!",
                role: "Doctor");

            var receptionistUser = await CreateUserIfNotExists(userManager,
                email: "receptionist@clinic.com",
                fullName: "Fatima Reception",
                password: "Admin123!",
                role: "Receptionist");

            var patientUser = await CreateUserIfNotExists(userManager,
                email: "patient@clinic.com",
                fullName: "Ali Patient",
                password: "Admin123!",
                role: "Patient");

            // Step 3: Create Patient profile linked to patient@clinic.com
            if (patientUser != null && !await context.Patients.AnyAsync(p => p.UserId == patientUser.Id))
            {
                var patient = new Patient
                {
                    UserId = patientUser.Id,
                    CPRNumber = "900101123",
                    PatientReferenceNumber = "PAT-0001",
                    DateOfBirth = new DateTime(1990, 1, 1),
                    Gender = "Male",
                    BloodType = "O+",
                    Allergies = "None",
                    Address = "Manama, Bahrain",
                    EmergencyContactName = "Emergency Contact",
                    EmergencyContactPhone = "39999999"
                };

                context.Patients.Add(patient);
                await context.SaveChangesAsync();
            }

            // Step 4: Create Doctor profile linked to doctor@clinic.com
            if (doctorUser != null && !await context.Doctors.AnyAsync(d => d.UserId == doctorUser.Id))
            {
                var doctor = new Doctor
                {
                    UserId = doctorUser.Id,
                    LicenseNumber = "DOC-0001",
                    Bio = "General clinic doctor",
                    YearsOfExperience = 8,
                    ConsultationFee = 15.000m,
                    IsAvailable = true
                };

                context.Doctors.Add(doctor);
                await context.SaveChangesAsync();
            }

            // Step 5: Create specializations if they do not exist
            if (!await context.Specializations.AnyAsync(s => s.Name == "General Medicine"))
            {
                context.Specializations.Add(new Specialization
                {
                    Name = "General Medicine",
                    Description = "General health checkups and common medical concerns."
                });
            }

            if (!await context.Specializations.AnyAsync(s => s.Name == "Cardiology"))
            {
                context.Specializations.Add(new Specialization
                {
                    Name = "Cardiology",
                    Description = "Heart and blood pressure related care."
                });
            }

            if (!await context.Specializations.AnyAsync(s => s.Name == "Dermatology"))
            {
                context.Specializations.Add(new Specialization
                {
                    Name = "Dermatology",
                    Description = "Skin, hair, and allergy related care."
                });
            }

            if (!await context.Specializations.AnyAsync(s => s.Name == "Pediatrics"))
            {
                context.Specializations.Add(new Specialization
                {
                    Name = "Pediatrics",
                    Description = "Medical care for children."
                });
            }

            await context.SaveChangesAsync();

            // Step 6: Link doctor to General Medicine specialization
            var seededDoctor = await context.Doctors
                .FirstOrDefaultAsync(d => d.LicenseNumber == "DOC-0001");

            var generalMedicine = await context.Specializations
                .FirstOrDefaultAsync(s => s.Name == "General Medicine");

            if (seededDoctor != null && generalMedicine != null)
            {
                bool alreadyLinked = await context.DoctorSpecializations.AnyAsync(ds =>
                    ds.DoctorId == seededDoctor.Id &&
                    ds.SpecializationId == generalMedicine.Id);

                if (!alreadyLinked)
                {
                    var doctorSpecialization = new DoctorSpecialization
                    {
                        DoctorId = seededDoctor.Id,
                        SpecializationId = generalMedicine.Id
                    };

                    context.DoctorSpecializations.Add(doctorSpecialization);
                    await context.SaveChangesAsync();
                }
            }

            // Step 7: Create sample completed visit record and prescription for patient testing
            var seededPatient = await context.Patients
                .FirstOrDefaultAsync(p => p.PatientReferenceNumber == "PAT-0001");

            var seededDoctorForVisit = await context.Doctors
                .FirstOrDefaultAsync(d => d.LicenseNumber == "DOC-0001");

            if (seededPatient != null &&
                seededDoctorForVisit != null &&
                !await context.Appointments.AnyAsync(a => a.AppointmentReferenceNumber == "APT-HISTORY-0001"))
            {
                var completedAppointment = new Appointment
                {
                    PatientId = seededPatient.Id,
                    DoctorId = seededDoctorForVisit.Id,
                    AppointmentReferenceNumber = "APT-HISTORY-0001",
                    AppointmentDate = DateTime.Today.AddDays(-7),
                    StartTime = new TimeSpan(9, 0, 0),
                    EndTime = new TimeSpan(9, 30, 0),
                    Status = AppointmentStatus.Completed,
                    ReasonForVisit = "Fever and headache",
                    CreatedByUserId = doctorUser?.Id ?? string.Empty,
                    CreatedAt = DateTime.UtcNow.AddDays(-7)
                };

                context.Appointments.Add(completedAppointment);
                await context.SaveChangesAsync();

                var visitRecord = new VisitRecord
                {
                    AppointmentId = completedAppointment.Id,
                    DoctorNotes = "Patient had mild fever and headache symptoms.",
                    Diagnosis = "Common cold",
                    TreatmentPlan = "Rest, fluids, and medication as prescribed.",
                    VisitDate = completedAppointment.AppointmentDate
                };

                context.VisitRecords.Add(visitRecord);
                await context.SaveChangesAsync();

                var prescription = new Prescription
                {
                    VisitRecordId = visitRecord.Id,
                    MedicationName = "Paracetamol",
                    Dosage = "500mg",
                    Frequency = "Twice daily",
                    Duration = "3 days",
                    Instructions = "Take after food."
                };

                context.Prescriptions.Add(prescription);
                await context.SaveChangesAsync();
            }
        }

        private static async Task<ApplicationUser?> CreateUserIfNotExists(
            UserManager<ApplicationUser> userManager,
            string email,
            string fullName,
            string password,
            string role)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(user, password);

                if (!result.Succeeded)
                {
                    return null;
                }
            }

            // Make sure the user has the correct role even if the account already existed
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }

            return user;
        }
    }
}