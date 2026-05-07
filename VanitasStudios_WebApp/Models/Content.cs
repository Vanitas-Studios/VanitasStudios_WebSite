using System;
using System.Collections.Generic;

namespace VanitasStudios_WebApp.Models;

public partial class Content
{
    public int IdC { get; set; }

    public string TypeC { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string DescC { get; set; } = null!;

    public bool IsPinned { get; set; } = false;

    public DateTime DataPub { get; set; }

    public DateTime? DataEdit { get; set; }

    public int EditorId { get; set; }

    public virtual ApplicationUser Editor { get; set; } = null!;

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual ICollection<Section> Sections { get; set; } = new List<Section>();

    public virtual ICollection<Tag> TagOrds { get; set; } = new List<Tag>();
}
