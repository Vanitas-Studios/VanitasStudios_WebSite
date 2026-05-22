using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VanitasStudios_WebApp.Models;

[Table("Sections")]
public partial class Section
{
    [Key]
    public int Id { get; set; }

    [StringLength(255)]
    public string Title { get; set; } = null!;

    [Required]
    public string HtmlText { get; set; } = null!;

    [Required]
    public int Order { get; set; }

    [Required]
    public int ContentId { get; set; }

    [ForeignKey("ContentId")]
    [InverseProperty("Sections")]
    public virtual Content Content { get; set; } = null!;

    // Collezione unificata dei file multimediali associati a questo blocco
    [InverseProperty("Section")]
    public virtual ICollection<Media> MediaElements { get; set; } = new List<Media>();

    //public virtual ICollection<Image> Images { get; set; } = new List<Image>();

    //public virtual ICollection<Video> Videos { get; set; } = new List<Video>();
}
