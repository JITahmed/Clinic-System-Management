namespace ClinicSystem.Api.Models
{
    public class Specialization
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Navigation properties
        public ICollection<DoctorSpecialization> DoctorSpecializations { get; set; } = new List<DoctorSpecialization>();
    }
}
