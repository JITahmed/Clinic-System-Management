namespace ClinicSystem.Reporting.DTOs
{
    public class LoginResponse
    {
        public string token { get; set; }
        public string email { get; set; }
        public List<string> roles { get; set; }
    }
}
