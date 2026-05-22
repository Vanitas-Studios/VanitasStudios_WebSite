using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VanitasStudios_WebApp.Models
{
    [Table("Media")]
    public partial class Media
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(512)]
        public string Url { get; set; } = string.Empty; // Sostituisce ImageUrl e VideoUrl

        [Required]
        public MediaType Type { get; set; } = MediaType.Image; // Dice all'HTML se renderizzare un tag <img> o <video>/<iframe>

        [StringLength(255)]
        public string? Caption { get; set; } // Una didascalia opzionale sotto il media, utilissima per le recensioni

        [Required]
        public bool IsThumbnail { get; set; } = false; // Se vero, può essere usata come anteprima della sezione

        [Required]
        public int Order { get; set; } = 0; // Ti permette di decidere se mettere prima il video o prima l'immagine nel paragrafo

        [Required]
        public int ReferenceCount { get; set; } = 1; // Il contatore per la GC dei file orfani

        // Il collegamento alla Sezione di testo
        [Required]
        public int SectionId { get; set; }

        [ForeignKey("SectionId")]
        [InverseProperty("MediaElements")]
        public virtual Section Section { get; set; } = null!;
    }
}
