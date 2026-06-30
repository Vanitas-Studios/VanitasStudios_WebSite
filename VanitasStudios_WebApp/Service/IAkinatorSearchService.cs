using VanitasStudios_WebApp.Models;

namespace VanitasStudios_WebApp.Service
{
    public interface IAkinatorSearchService
    {
        // Metodo per l'autocomplete dinamico mentre l'utente digita
        Task<List<AutocompleteTagDto>> GetTagSuggestionsAsync(string term, int maxSuggestions = 5);

        // Metodo principale per calcolare la ricerca a bivi (Akinator)
        Task<AkinatorResultDto> ExecuteSearchAsync(string userText, List<int> selectedTagIds);
    }

    // DTO di supporto per l'autocomplete
    public class AutocompleteTagDto
    {
        public int TagId { get; set; }
        public string TagName { get; set; } = null!;
        public string? Category { get; set; }
        public int MaxArticleWeight { get; set; }
    }

    public class AkinatorResultDto
    {
        // Se è TRUE, l'algoritmo ha trovato un vincitore chiaro e lo mostra.
        // Se è FALSE, i punteggi sono troppo vicini ed entra in modalità "Akinator" (fa una domanda).
        public bool IsFinalResult { get; set; }

        // Questa lista conterrà gli articoli reali. 
        // Verrà popolata SOLO se IsFinalResult è TRUE.
        public List<SearchArticleResultDto> Articles { get; set; } = new();

        // Se IsFinalResult è FALSE, qua dentro ci mettiamo l'ID del prossimo tag consigliato
        // su cui l'utente dovrà esprimere la sua preferenza.
        public int? NextTagIdSuggested { get; set; }

        // Il testo della domanda da mostrare sotto la barra (es: "#Planche")
        public string NextQuestionText { get; set; } = string.Empty;
    }

    public class SearchArticleResultDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string Slug { get; set; } = null!;
        public string? CoverImageUrl { get; set; }
        public string FormattedDate { get; set; } = string.Empty;
    }
}
