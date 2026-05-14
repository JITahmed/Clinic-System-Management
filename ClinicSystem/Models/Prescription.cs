namespace ClinicSystem.Api.Models
{
    public class Prescription
    {
        public int Id { get; set; }
        public int VisitRecordId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string Dosage { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;

        // Navigation properties
        public VisitRecord VisitRecord { get; set; } = null!;
    }
}
