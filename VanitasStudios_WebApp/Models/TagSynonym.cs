using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VanitasStudios_WebApp.Models
{
    [Table("TagSynonyms")]
    public partial class TagSynonym
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [StringLength(100)]
        public string SynonymName { get; set; } = string.Empty; // Es: "GDR", "RPG"

        // La chiave esterna che punta al Tag principale
        [Required]
        public int TagId { get; set; }

        [ForeignKey("TagId")]
        [InverseProperty("Synonyms")]
        public virtual Tag Tag { get; set; } = null!; // Il tag principale (Es: "Role-Playing Game")
    }
}
