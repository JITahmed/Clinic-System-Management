using ClinicSystem.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClinicSystem.Api.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // These are your database tables
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Specialization> Specializations { get; set; }
        public DbSet<DoctorSpecialization> DoctorSpecializations { get; set; }
        public DbSet<DoctorSchedule> DoctorSchedules { get; set; }
        public DbSet<DoctorLeave> DoctorLeaves { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<VisitRecord> VisitRecords { get; set; }
        public DbSet<Prescription> Prescriptions { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // DoctorSpecialization — composite primary key (no single Id column)
            modelBuilder.Entity<DoctorSpecialization>()
                .HasKey(ds => new { ds.DoctorId, ds.SpecializationId });

            // DoctorSpecialization to Doctor
            modelBuilder.Entity<DoctorSpecialization>()
                .HasOne(ds => ds.Doctor)
                .WithMany(d => d.DoctorSpecializations)
                .HasForeignKey(ds => ds.DoctorId);

            // DoctorSpecialization to Specialization
            modelBuilder.Entity<DoctorSpecialization>()
                .HasOne(ds => ds.Specialization)
                .WithMany(s => s.DoctorSpecializations)
                .HasForeignKey(ds => ds.SpecializationId);

            // Patient to ApplicationUser
            modelBuilder.Entity<Patient>()
                .HasOne(p => p.User)
                .WithOne(u => u.Patient)
                .HasForeignKey<Patient>(p => p.UserId);

            // Doctor to ApplicationUser
            modelBuilder.Entity<Doctor>()
                .HasOne(d => d.User)
                .WithOne(u => u.Doctor)
                .HasForeignKey<Doctor>(d => d.UserId);

            // Appointment to Patient
            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Patient)
                .WithMany(p => p.Appointments)
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Appointment to Doctor
            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Doctor)
                .WithMany(d => d.Appointments)
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            // VisitRecord to Appointment
            modelBuilder.Entity<VisitRecord>()
                .HasOne(v => v.Appointment)
                .WithOne(a => a.VisitRecord)
                .HasForeignKey<VisitRecord>(v => v.AppointmentId);

            // Prescription to VisitRecord
            modelBuilder.Entity<Prescription>()
                .HasOne(p => p.VisitRecord)
                .WithMany(v => v.Prescriptions)
                .HasForeignKey(p => p.VisitRecordId);

            // Notification to ApplicationUser
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId);

            // Decimal precision for consultation fee
            modelBuilder.Entity<Doctor>()
                .Property(d => d.ConsultationFee)
                .HasColumnType("decimal(18,2)");

            // Unique constraints
            modelBuilder.Entity<Patient>()
                .HasIndex(p => p.CPRNumber)
                .IsUnique();

            modelBuilder.Entity<Patient>()
                .HasIndex(p => p.PatientReferenceNumber)
                .IsUnique();

            modelBuilder.Entity<Doctor>()
                .HasIndex(d => d.LicenseNumber)
                .IsUnique();
        }
    }
}