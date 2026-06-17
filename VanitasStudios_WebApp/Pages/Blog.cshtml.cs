using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VanitasStudios_WebApp.Data;
using VanitasStudios_WebApp.Models;
using VanitasStudios_WebApp.Service;

namespace VanitasStudios_WebApp.Pages
{
    public class BlogModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IAkinatorSearchService _searchService;

        // Proprietà per il caricamento iniziale sincrono (Stile Netflix)
        public List<Content> LatestContents { get; set; } = new();

        // Un dizionario che raggruppa gli articoli per il nome della macro-categoria
        public Dictionary<string, List<Content>> CategorizedContents { get; set; } = new();

        public BlogModel(ApplicationDbContext context, IAkinatorSearchService searchService)
        {
            _context = context;
            _searchService = searchService;
        }

        // ------------------------------------------------------------------
        // CARICAMENTO INIZIALE DELLA PAGINA (Sincrono)
        // ------------------------------------------------------------------
        public async Task OnGetAsync()
        {
            // 1. Recuperiamo tutti i contenuti pubblicati includendo i tag e le loro categorie
            var allPublishedContents = await _context.Contents
                .Include(c => c.ContentTags)
                    .ThenInclude(ct => ct.Tag) // Assicurati che la tua tabella di giunzione punti all'oggetto Tag reale
                .Where(c => c.PublishState == PublishState.Pubblico) // Sostituisci con la tua Enum o condizione di stato
                .OrderByDescending(c => c.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            // 2. Riga "Ultimi Arrivi": prendiamo i primi 6/8 articoli più freschi in assoluto
            LatestContents = allPublishedContents.Take(8).ToList();

            // 3. Righe per Categoria: raggruppiamo gli articoli in base al CategoryGroup dei tag associati
            // Nota: se un articolo ha tag di categorie diverse, comparirà in più gruppi!
            CategorizedContents = allPublishedContents
                .SelectMany(c => c.ContentTags.Select(ct => new { Category = ct.Tag.CategoryGroup, Content = c }))
                .Where(x => !string.IsNullOrEmpty(x.Category))
                .GroupBy(x => x.Category)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.Content).Distinct().Take(6).ToList() // Evitiamo duplicati nella stessa riga e limitiamo a 6
                );
        }

        // ------------------------------------------------------------------
        // HANDLER 1: Autocomplete dinamico (Asincrono via AJAX)
        // ------------------------------------------------------------------
        public async Task<JsonResult> OnGetSearchAutocompleteAsync(string term)
        {
            var suggestions = await _searchService.GetTagSuggestionsAsync(term, maxSuggestions: 5);
            return new JsonResult(suggestions);
        }

        // ------------------------------------------------------------------
        // HANDLER 2: Esecuzione Ricerca / Bivio Akinator (Asincrono via AJAX)
        // ------------------------------------------------------------------
        public async Task<JsonResult> OnGetExecuteAkinatorSearchAsync(string userText, List<int> selectedTagIds)
        {
            AkinatorResultDto result = await _searchService.ExecuteSearchAsync(userText, selectedTagIds);
            return new JsonResult(result);
        }
    }
}