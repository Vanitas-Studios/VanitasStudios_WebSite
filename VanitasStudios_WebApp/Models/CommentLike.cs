using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace VanitasStudios_WebApp.Models;
[Table("CommentLikes")]
public partial class CommentLike
{
    //Identifica l'utente che mette il like
    [Key]
    [Column(Order = 1)]
    [Required]
    public int UserId { get; set; }

    //Identifica il commento che riceve il like
    [Key]
    [Column(Order = 2)]
    [Required]
    public int CommentId { get; set; }

    // true = Like, false = Dislike
    [Required]
    public bool IsLike { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("GivenCommentLikes")]
    public virtual ApplicationUser User { get; set; } = null!;

    [ForeignKey("CommentId")]
    public virtual Comment Comment { get; set; } = null!;
}
