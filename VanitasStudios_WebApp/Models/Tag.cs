using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VanitasStudios_WebApp.Models;

[Table("Tags")]
public partial class Tag
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)] // Un tag difficilmente supererà i 100 caratteri
    public string Name { get; set; } = null!;

    //public string TypeT { get; set; } = null!; Nessuna distinzione
    [StringLength(100)]
    public string? CategoryGroup { get; set; } // Aggiunto per il raggruppamento logico

    [InverseProperty("Tag")]
    public virtual ICollection<ContentTag> ContentTags { get; set; } = new List<ContentTag>();
    [InverseProperty("Tag")]
    public virtual ICollection<TagSynonym> Synonyms { get; set; } = new List<TagSynonym>();
}
