using ClinicSystem.Api.Data;
using ClinicSystem.Api.Models;

namespace ClinicSystem.Api.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SendAsync(
            string userId,
            string title,
            string message,
            NotificationType type,
            int? relatedEntityId = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                IsRead = false,
                RelatedEntityId = relatedEntityId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task AppointmentBookedAsync(
            string patientUserId,
            string doctorUserId,
            string patientName,
            string doctorName,
            DateTime appointmentDate,
            int appointmentId)
        {
            // Notify the doctor
            await SendAsync(
                doctorUserId,
                "New Appointment Request",
                $"{patientName} has requested an appointment on {appointmentDate:dd MMM yyyy}.",
                NotificationType.AppointmentBooked,
                appointmentId);

            // Notify the patient
            await SendAsync(
                patientUserId,
                "Appointment Request Submitted",
                $"Your appointment request with Dr. {doctorName} on {appointmentDate:dd MMM yyyy} has been submitted.",
                NotificationType.AppointmentBooked,
                appointmentId);
        }

        public async Task AppointmentConfirmedAsync(
            string patientUserId,
            string doctorUserId,
            string doctorName,
            DateTime appointmentDate,
            int appointmentId)
        {
            // Notify the patient
            await SendAsync(
                patientUserId,
                "Appointment Confirmed",
                $"Your appointment with Dr. {doctorName} on {appointmentDate:dd MMM yyyy} has been confirmed.",
                NotificationType.AppointmentConfirmed,
                appointmentId);

            // Notify the doctor
            await SendAsync(
                doctorUserId,
                "Appointment Confirmed",
                $"Appointment on {appointmentDate:dd MMM yyyy} has been confirmed.",
                NotificationType.AppointmentConfirmed,
                appointmentId);
        }

        public async Task AppointmentCancelledAsync(
            string patientUserId,
            string doctorUserId,
            string patientName,
            string doctorName,
            DateTime appointmentDate,
            int appointmentId)
        {
            // Notify the patient
            await SendAsync(
                patientUserId,
                "Appointment Cancelled",
                $"Your appointment with Dr. {doctorName} on {appointmentDate:dd MMM yyyy} has been cancelled.",
                NotificationType.AppointmentCancelled,
                appointmentId);

            // Notify the doctor
            await SendAsync(
                doctorUserId,
                "Appointment Cancelled",
                $"Appointment with {patientName} on {appointmentDate:dd MMM yyyy} has been cancelled.",
                NotificationType.AppointmentCancelled,
                appointmentId);
        }

        public async Task AppointmentCheckedInAsync(
            string doctorUserId,
            string patientName,
            DateTime appointmentDate,
            int appointmentId)
        {
            // Notify the doctor only
            await SendAsync(
                doctorUserId,
                "Patient Checked In",
                $"{patientName} has checked in for their {appointmentDate:dd MMM yyyy} appointment.",
                NotificationType.AppointmentCheckedIn,
                appointmentId);
        }

        public async Task AppointmentCompletedAsync(
            string patientUserId,
            string doctorName,
            DateTime appointmentDate,
            int appointmentId)
        {
            // Notify the patient only
            await SendAsync(
                patientUserId,
                "Appointment Completed",
                $"Your appointment with Dr. {doctorName} on {appointmentDate:dd MMM yyyy} is complete. Your visit record is now available.",
                NotificationType.AppointmentCompleted,
                appointmentId);
        }

        public async Task AppointmentMissedAsync(
            string patientUserId,
            string doctorName,
            DateTime appointmentDate,
            int appointmentId)
        {
            // Notify the patient only
            await SendAsync(
                patientUserId,
                "Appointment Missed",
                $"You missed your appointment with Dr. {doctorName} on {appointmentDate:dd MMM yyyy}. Please rebook if needed.",
                NotificationType.AppointmentMissed,
                appointmentId);
        }
    }
}