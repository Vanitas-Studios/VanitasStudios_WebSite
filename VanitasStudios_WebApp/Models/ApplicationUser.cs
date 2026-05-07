using Microsoft.AspNetCore.Identity;

namespace VanitasStudios_WebApp.Models
{
    public class ApplicationUser : IdentityUser<int>
    {
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

        public virtual ICollection<Content> Contents { get; set; } = new List<Content>();

        public virtual ICollection<Evaluate> Evaluates { get; set; } = new List<Evaluate>();
    }
}
