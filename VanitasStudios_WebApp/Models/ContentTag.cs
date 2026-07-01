using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VanitasStudios_WebApp.Models
{
    [Table("ContentTags")]
    public partial class ContentTag
    {
        [Key]
        [Column(Order = 1)]
        [Required]
        public int ContentId { get; set; }

        [Key]
        [Column(Order = 2)]
        [Required]
        public int TagId { get; set; }

        // Il peso algoritmico del tag su questo specifico articolo (es. da 0.0 a 1.0)
        // Fondamentale per l'albero di decisione dell'IA
        [Required]
        public float Weight { get; set; } = 0.0f;

        [ForeignKey("ContentId")]
        public virtual Content Content { get; set; } = null!;

        [ForeignKey("TagId")]
        public virtual Tag Tag { get; set; } = null!;
    }
}
