namespace ClinicSystem.Api.DTOs
{
    public class PublicLookupResponse
    {
        public List<UpcomingAppointment> Upcoming { get; set; } = new();
        public List<RecentVisit> RecentVisits { get; set; } = new();
    }

    public class UpcomingAppointment
    {
        public int Id { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Status { get; set; }
        public string DoctorName { get; set; }
    }

    public class RecentVisit
    {
        public int Id { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Diagnosis { get; set; }
    }
}
