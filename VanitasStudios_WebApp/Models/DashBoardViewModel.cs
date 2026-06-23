namespace VanitasStudios_WebApp.Models
{
    public class DashboardViewModel
    {
        // Conteggi generali articoli
        public int TotalArticlesOnline { get; set; }
        public int TotalArticlesDraft { get; set; }

        // Metriche di Performance dell'Algoritmo (da SearchHistory)
        public int TotalSearchesExecuted { get; set; }
        public double AlgorithmSuccessRate { get; set; } // Percentuale di successo (es: 84.5%)

        // Top 3 Articoli di Tendenza
        public List<TopTrendingArticleDto> TopArticles { get; set; } = new();

        // Top 5 Argomenti/Tag più caldi
        public List<TopTagDto> TopTags { get; set; } = new();
    }

    // 2. I DTO di supporto stanno fuori, ma nello stesso file/namespace
    public class TopTrendingArticleDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public int CumulativeWeight { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class TopTagDto
    {
        public int TagId { get; set; }
        public string TagName { get; set; } = null!;
        public string? Category { get; set; }
        public int TotalGlobalWeight { get; set; }
    }
}
