using Microsoft.AspNetCore.Identity;

namespace StudyApp.Models
{
    public class User : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
    }
}