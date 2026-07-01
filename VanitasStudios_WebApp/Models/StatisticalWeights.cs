using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace VanitasStudios_WebApp.Models
{
    [Table("StatisticalWeights")]
    public partial class StatisticalWeights
    {
        [Required]
        public int TagId { get; set; } 

        [Required]
        public int ContentId { get; set; } 

        [Required]
        public int PopularityWeight { get; set; } = 0; // Contatore cumulativo dei click 

        [ForeignKey("TagId")]
        public virtual Tag Tag { get; set; } = null!;

        [ForeignKey("ContentId")]
        public virtual Content Content { get; set; } = null!;
    }
}
