namespace ClinicSystem.Api.Models
{
    public class VisitRecord
    {
        public int Id { get; set; }
        public int AppointmentId { get; set; }
        public string DoctorNotes { get; set; } = string.Empty;
        public string Diagnosis { get; set; } = string.Empty;
        public string TreatmentPlan { get; set; } = string.Empty;
        public DateTime VisitDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Appointment Appointment { get; set; } = null!;
        public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
    }
}