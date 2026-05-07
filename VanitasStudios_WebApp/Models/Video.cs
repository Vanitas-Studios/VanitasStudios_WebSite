using System;
using System.Collections.Generic;

namespace VanitasStudios_WebApp.Models;

public partial class Video
{
    public int IdV { get; set; }

    public string VideoUrl { get; set; } = null!;

    public int SectionVideoId { get; set; }

    public int ImageVideoId { get; set; }

    public virtual Image ImageVideo { get; set; } = null!;

    public virtual Section SectionVideo { get; set; } = null!;
}
