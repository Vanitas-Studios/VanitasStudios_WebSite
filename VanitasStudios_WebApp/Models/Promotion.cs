using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VanitasStudios_WebApp.Models;

[Table("Promotions")]
public partial class Promotion
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int PromotedId { get; set; }

    [Required]
    public int PromoterId { get; set; }

    [ForeignKey("PromotedId")]
    public virtual ApplicationUser Promoted { get; set; } = null!;

    [ForeignKey("PromoterId")]
    public virtual ApplicationUser Promoter { get; set; } = null!;

    [Required]
    public DateTime PromotedAt { get; set; } = DateTime.UtcNow;
}
