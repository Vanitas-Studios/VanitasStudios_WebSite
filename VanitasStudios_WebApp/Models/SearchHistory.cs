using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VanitasStudios_WebApp.Models
{
    [Table("SearchHistory")]
    public partial class SearchHistory
    {
        [Key]
        public int Id { get; set; } 

        public int? UserId { get; set; } // NULL per utenti ospiti anonimi 

        [Required]
        public string QueryTags { get; set; } = string.Empty; // Vettore JSON dei tag selezionati 

        public int? ResultContentId { get; set; } // Articolo finale selezionato

        [Required]
        public bool IsSuccessful { get; set; } = false; // Feedback di validazione

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow; 

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        [ForeignKey("ResultContentId")]
        public virtual Content? ResultContent { get; set; }
    }
}
