namespace ClinicSystem.Api.Models
{
    public class Patient
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string CPRNumber { get; set; } = string.Empty;

        public string PatientReferenceNumber { get; set; } = string.Empty;

        public DateTime DateOfBirth { get; set; }

        public string Gender { get; set; } = string.Empty;

        public string BloodType { get; set; } = string.Empty;

        public string Allergies { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string EmergencyContactName { get; set; } = string.Empty;

        public string EmergencyContactPhone { get; set; } = string.Empty;

        // Navigation properties
        public ApplicationUser User { get; set; } = null!;

        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}