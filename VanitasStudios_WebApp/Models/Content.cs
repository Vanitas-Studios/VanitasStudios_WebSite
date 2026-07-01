using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VanitasStudios_WebApp.Models;

[Table("Contents")]
public partial class Content
{
    public int Id { get; set; }

    //public string TypeC { get; set; } = null!; Non usiamo più alcuna distinzione per i Tag

    [Required]
    [StringLength(255)]
    public string Title { get; set; } = null!;

    [Required]
    [StringLength(255)]
    public string Slug { get; set; } = string.Empty; // URL ottimizzato SEO (es. "il-game-design-di-dark-souls")

    public string? Description { get; set; } = null!;

    [StringLength(512)]
    public string? CoverImageUrl { get; set; } // URL o percorso dell'immagine di copertina dell'Hero

    [Required]
    public bool IsPinned { get; set; } = false;

    [Required]
    public PublishState PublishState { get; set; } = PublishState.Bozza; // Gestisce Bozza, Pubblico, Eliminato

    [Required]
    public float GlobalScore { get; set; } = 0.0f; // Calcolato periodicamente per Akinator IA

    [Required]
    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? EliminatedAt { get; set; } // Data di soft-delete per il countdown di 30gg

    [Required]
    public int AuthorId { get; set; }

    [ForeignKey("AuthorId")]
    [InverseProperty("AuthoredArticles")]
    public virtual ApplicationUser Author { get; set; } = null!;

    [InverseProperty("Content")]
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    [InverseProperty("Content")]
    public virtual ICollection<Section> Sections { get; set; } = new List<Section>();

    // Relazione verso la tabella di giunzione pesata per i Tag dell'IA
    public virtual ICollection<ContentTag> ContentTags { get; set; } = new List<ContentTag>();
}
