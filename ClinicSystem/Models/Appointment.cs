namespace ClinicSystem.Api.Models
{
    public enum AppointmentStatus
    {
        Requested,
        Confirmed,
        CheckedIn,
        InProgress,
        Completed,
        Cancelled,
        Missed
    }

    public class Appointment
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public string AppointmentReferenceNumber { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Requested;
        public string ReasonForVisit { get; set; } = string.Empty;
        public string CancellationReason { get; set; } = string.Empty;
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Patient Patient { get; set; } = null!;
        public Doctor Doctor { get; set; } = null!;
        public VisitRecord? VisitRecord { get; set; }
    }
}
