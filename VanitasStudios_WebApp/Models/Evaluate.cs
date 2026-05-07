using System;
using System.Collections.Generic;

namespace VanitasStudios_WebApp.Models;

public partial class Evaluate
{
    public int UserLikeId { get; set; }

    public int CommLikeId { get; set; }

    public bool IsLike { get; set; }

    public virtual ApplicationUser UserLike { get; set; } = null!;

    public virtual Comment CommLike { get; set; } = null!;
}
