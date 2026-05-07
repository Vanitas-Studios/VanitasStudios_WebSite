using System;
using System.Collections.Generic;

namespace VanitasStudios_WebApp.Models;

public partial class Promotion
{
    public int IdPromotion { get; set; }

    public int PromotedId { get; set; }

    public int AdminPromoterId { get; set; }

    public virtual ApplicationUser Promoted { get; set; } = null!;

    public virtual ApplicationUser AdminPromoter { get; set; } = null!;

    public DateTime DataPromotion { get; set; }
}
