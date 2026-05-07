using System;
using System.Collections.Generic;

namespace VanitasStudios_WebApp.Models;

public partial class Comment
{
    public int IdComm { get; set; }

    public string CommText { get; set; } = null!;

    public DateTime DataPub { get; set; }

    public int ContentId { get; set; }

    public int CommentUserId { get; set; }

    public int? AnswerId { get; set; }

    public virtual ApplicationUser User { get; set; } = null!;

    public virtual Comment? Answer { get; set; }

    public virtual Content Content { get; set; } = null!;

    public virtual ICollection<Evaluate> Evaluates { get; set; } = new List<Evaluate>();

    public virtual ICollection<Comment> InverseAnswer { get; set; } = new List<Comment>();
}
