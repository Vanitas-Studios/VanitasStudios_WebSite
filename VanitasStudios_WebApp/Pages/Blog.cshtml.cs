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
    [IgnoreAntiforgeryToken]
    public class BlogModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IAkinatorSearchService _searchService;

        // Proprietà per il caricamento iniziale sincrono (Stile Netflix)
        public List<Content> LatestContents { get; set; } = new();

        // Un dizionario che raggruppa gli articoli per il nome della macro-categoria
        public Dictionary<string, List<Content>> CategorizedContents { get; set; } = new();

        // Classe di supporto per mappare il payload in arrivo dal JS
        public class TrackingPayload
        {
            public string textSearch { get; set; } = string.Empty;
            public List<TagPayload> tags { get; set; } = new();
        }

        public class TagPayload
        {
            public int id { get; set; }
            public string name { get; set; } = string.Empty;
        }

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

        public async Task<IActionResult> OnPostRecordSearchSuccessAsync(int articleId, string searchQuery)
        {
            if (articleId <= 0 || string.IsNullOrWhiteSpace(searchQuery))
            {
                return BadRequest(new { error = "Dati incompleti o non validi." });
            }

            try
            {
                // 1. Recuperiamo l'articolo dal database
                var articolo = await _context.Contents.FindAsync(articleId);
                if (articolo == null)
                {
                    return NotFound(new { error = "Frammento non trovato." });
                }

                // 2. Incrementiamo il GlobalScore dell'articolo
                articolo.GlobalScore += 1.0f;
                articolo.UpdatedAt = DateTime.UtcNow;

                // 3. Estraiamo l'ID dell'utente se loggato
                int? currentUserId = null;
                if (User.Identity != null && User.Identity.IsAuthenticated)
                {
                    var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(userIdClaim, out int parsedId))
                    {
                        currentUserId = parsedId;
                    }
                }

                // Deserializziamo il payload strutturato inviato dal JS
                var payload = System.Text.Json.JsonSerializer.Deserialize<TrackingPayload>(searchQuery);

                // Prepariamo la stringa dei tag ottimizzata per la visualizzazione dell'Admin
                string adminDisplayString = "";
                if (payload != null && payload.tags.Any())
                {
                    // Uniamo i nomi dei tag separati da virgola, racchiusi in parentesi quadre (Es: [Elden Ring, Lore])
                    adminDisplayString = $"[{string.Join(", ", payload.tags.Select(t => t.name))}]";
                }
                else
                {
                    adminDisplayString = !string.IsNullOrWhiteSpace(payload?.textSearch)
                        ? $"[\"{payload.textSearch}\"]"
                        : "[Esplorazione Casuale]";
                }

                // 4. Registriamo la ricerca nella cronologia generale
                var searchRecord = new SearchHistory
                {
                    UserId = currentUserId,
                    QueryTags = adminDisplayString, // Salvato in formato leggibile per la parziale Analytics dell'Admin
                    ResultContentId = articleId,
                    IsSuccessful = true,
                    Timestamp = DateTime.UtcNow
                };
                _context.SearchHistories.Add(searchRecord);

                // 5. AGGIORNAMENTO PESI AKINATOR (StatisticalWeights)
                if (payload != null && payload.tags.Any())
                {
                    foreach (var tagObj in payload.tags)
                    {
                        // Cerchiamo se esiste già il record di peso usando direttamente il TagId numerico sicuro
                        var weightRecord = await _context.StatisticalWeights
                            .FirstOrDefaultAsync(w => w.TagId == tagObj.id && w.ContentId == articleId);

                        if (weightRecord != null)
                        {
                            weightRecord.PopularityWeight += 1;
                        }
                        else
                        {
                            var newWeight = new StatisticalWeights
                            {
                                TagId = tagObj.id,
                                ContentId = articleId,
                                PopularityWeight = 1
                            };
                            _context.StatisticalWeights.Add(newWeight);
                        }
                    }
                }

                // 6. Salviamo le modifiche sul database
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true, newGlobalScore = articolo.GlobalScore });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR RECORD SEARCH SUCCESS]: {ex.Message}");
                return StatusCode(500, new { error = "Errore durante l'aggiornamento delle metriche dell'algoritmo." });
            }
        }

    }
}