using Microsoft.AspNetCore.Identity;
using System.Numerics;

namespace ClinicSystem.Api.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public Patient? Patient { get; set; }
        public Doctor? Doctor { get; set; }
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}
