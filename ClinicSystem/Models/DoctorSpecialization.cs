namespace ClinicSystem.Api.Models
{
    // Junction table — links Doctor to Specialization (many-to-many)
    public class DoctorSpecialization
    {
        public int DoctorId { get; set; }
        public int SpecializationId { get; set; }

        // Navigation properties
        public Doctor Doctor { get; set; } = null!;
        public Specialization Specialization { get; set; } = null!;
    }
}
