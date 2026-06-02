using System.ComponentModel.DataAnnotations;

namespace ClinicSystem.web.Models
{
    public class ConsultationViewModel
    {
        public int AppointmentId { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string ReasonForVisit { get; set; } = string.Empty;
        public DateTime PatientDateOfBirth { get; set; }
        public string BloodType { get; set; } = string.Empty;
        public string Allergies { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter clinical observations")]
        public string Symptoms { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a diagnosis")]
        public string DiagnosisText { get; set; } = string.Empty;

        public string TreatmentPlan { get; set; } = string.Empty;
        public string? MedicationName { get; set; }
        public string? Dosage { get; set; }
        public string? Frequency { get; set; }
        public string? Duration { get; set; }
        public string? Instructions { get; set; }
    }
}