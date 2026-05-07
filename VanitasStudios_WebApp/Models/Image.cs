using System;
using System.Collections.Generic;

namespace VanitasStudios_WebApp.Models;

public partial class Image
{
    public int IdI { get; set; }

    public string ImageUrl { get; set; } = null!;

    public bool IsThumbnail { get; set; }

    public int SectionImageId { get; set; }

    public virtual Section SectionImage { get; set; } = null!;

    public virtual Video? Video { get; set; }
}
