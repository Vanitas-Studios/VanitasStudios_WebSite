namespace VanitasStudios_WebApp.Models
{
    public class AkinatorAnalyticsViewModel
    {
        // Metriche macro in alto
        public int TotalSearches { get; set; }
        public double SuccessRate { get; set; } // Percentuale di ricerche andate a buon fine
        public int TotalBounces { get; set; } // Utenti rimasti a bocca asciutta

        // I dati per i due blocchi principali della UI
        public List<SearchHistoryRowDto> RecentQueries { get; set; } = new();
        public List<GhostTermDto> TopGhostTerms { get; set; } = new();
    }

    public class SearchHistoryRowDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string QueryTags { get; set; } = null!; // Stringa di tag accumulati nella sessione
        public string? MatchedContentTitle { get; set; } // Titolo dell'articolo trovato (se c'è)
        public bool IsSuccessful { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class GhostTermDto
    {
        public string Term { get; set; } = null!;
        public int SearchCount { get; set; }
        public DateTime LastSearched { get; set; }
    }
}
