using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VanitasStudios_WebApp.Models
{
    [Table("AdminLogs")]
    public class AdminLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; } // Chi ha fatto l'azione

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string ActionType { get; set; } = null!; // es. "DELETE_ARTICLE", "PROMOTE_USER", "UPDATE_WEIGHT"

        [Required]
        public string Description { get; set; } = null!; // es. "Eliminato l'articolo 'Il game design di Sekiro'"

        [Required]
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

        [StringLength(45)]
        public string? IpAddress { get; set; } // Opzionale, utile per la sicurezza
    }
}

