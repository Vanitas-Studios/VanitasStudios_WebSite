using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace VanitasStudios_WebApp.Models
{
    public class ApplicationUser : IdentityUser<int>
    {
        [InverseProperty("User")]
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

        [InverseProperty("Author")]
        public virtual ICollection<Content> AuthoredArticles { get; set; } = new List<Content>();

        [InverseProperty("User")]
        public virtual ICollection<CommentLike> GivenCommentLikes { get; set; } = new List<CommentLike>();

        // Le promozioni che questo utente ha RICEVUTO nel tempo
        [InverseProperty("Promoted")]
        public virtual ICollection<Promotion> ReceivedPromotions { get; set; } = new List<Promotion>();

        // Le promozioni che questo utente ha CONCESSO ad altri (se è un Admin)
        [InverseProperty("Promoter")]
        public virtual ICollection<Promotion> GrantedPromotions { get; set; } = new List<Promotion>();
    }
}
