using System;
using System.Collections.Generic;

namespace VanitasStudios_WebApp.Models;

public partial class Section
{
    public int IdS { get; set; }

    public string Title { get; set; } = null!;

    public string SectionText { get; set; } = null!;

    public int OrderNum { get; set; }

    public int ContentSId { get; set; }

    public virtual Content ContentS { get; set; } = null!;

    public virtual ICollection<Image> Images { get; set; } = new List<Image>();

    public virtual ICollection<Video> Videos { get; set; } = new List<Video>();
}
