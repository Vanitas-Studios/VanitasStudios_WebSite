using System;
using System.Collections.Generic;

namespace VanitasStudios_WebApp.Models;

public partial class Tag
{
    public int IdT { get; set; }

    public string TagName { get; set; } = null!;

    public string TypeT { get; set; } = null!;

    public virtual ICollection<Content> ContentOrds { get; set; } = new List<Content>();
}
