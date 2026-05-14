namespace ClinicSystem.Api.Models
{
    public enum NotificationType
    {
        AppointmentBooked,
        AppointmentConfirmed,
        AppointmentCancelled,
        AppointmentReminder,
        AppointmentCheckedIn,
        AppointmentCompleted,
        AppointmentMissed,
        General
    }

    public class Notification
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; } = NotificationType.General;
        public bool IsRead { get; set; } = false;
        public int? RelatedEntityId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ApplicationUser User { get; set; } = null!;
    }
}
