using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VanitasStudios_WebApp.Models;
[Table("Comments")]
public partial class Comment
{
    [Key]
    public int Id { get; set; }

    [Required]
    // Mettiamo un limite ragionevole al testo di un commento (es. 2000 caratteri) per evitare che intasino il DB
    [StringLength(2000)]
    public string Text { get; set; } = null!;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public int ContentId { get; set; }

    [Required]
    public int UserId { get; set; }

    public int? ParentCommentId { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("Comments")]
    public virtual ApplicationUser User { get; set; } = null!;

    [ForeignKey("ParentCommentId")]
    public virtual Comment? ParentComment { get; set; }

    [ForeignKey("ContentId")]
    public virtual Content Content { get; set; } = null!;

    [InverseProperty("Comment")]
    public virtual ICollection<CommentLike> CommentLikes { get; set; } = new List<CommentLike>();

    [InverseProperty("ParentComment")]
    public virtual ICollection<Comment> Replies { get; set; } = new List<Comment>();
}
