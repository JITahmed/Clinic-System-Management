namespace ClinicSystem.web.DTOs
{
    public class LookupResult
    {
        public List<AppointmentSummary> Upcoming { get; set; }
        public List<VisitSummary> RecentVisits { get; set; }
    }

    public class AppointmentSummary
    {
        public int Id { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Status { get; set; }
        public string DoctorName { get; set; }
    }

    public class VisitSummary
    {
        public int Id { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Diagnosis { get; set; }
    }
}
