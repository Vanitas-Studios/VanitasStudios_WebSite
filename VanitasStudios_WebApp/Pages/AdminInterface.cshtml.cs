using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using VanitasStudios_WebApp.Data;
using VanitasStudios_WebApp.Models;

namespace VanitasStudios_WebApp.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminInterfaceModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public class DeleteArticleRequest
        {
            public int Id { get; set; }
        }

        public AdminInterfaceModel(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            _context = dbContext;
            _userManager = userManager;
        }
        // Al primo caricamento restituisce la struttura fissa
        public void OnGet()
        {
        }

        // Handler per la vista "Dashboard Generale"
        public async Task<PartialViewResult> OnGetGeneralDashboardAsync()
        {
            var viewModel = new DashboardViewModel();

            try
            {
                // 1. Conteggio articoli divisi per Stato
                viewModel.TotalArticlesOnline = await _context.Contents
                    .CountAsync(c => c.PublishState == PublishState.Pubblico);

                viewModel.TotalArticlesDraft = await _context.Contents
                    .CountAsync(c => c.PublishState == PublishState.Bozza);

                // 2. Metriche dell'algoritmo Akinator dalla cronologia delle ricerche
                viewModel.TotalSearchesExecuted = await _context.SearchHistories.CountAsync();

                if (viewModel.TotalSearchesExecuted > 0)
                {
                    int successfulSearches = await _context.SearchHistories.CountAsync(sh => sh.IsSuccessful);
                    // Calcolo la percentuale di successo dell'algoritmo
                    viewModel.AlgorithmSuccessRate = Math.Round(((double)successfulSearches / viewModel.TotalSearchesExecuted) * 100, 1);
                }
                else
                {
                    viewModel.AlgorithmSuccessRate = 0.0;
                }

                // 3. Gli articoli PIÙ APPREZZATI / DI TENDENZA
                // Calcolati sommando i pesi cumulativi dei click (PopularityWeight) nella tabella pivot
                viewModel.TopArticles = await _context.StatisticalWeights
                    .Where(sw => sw.Content.PublishState == PublishState.Pubblico)
                    .GroupBy(sw => new { sw.ContentId, sw.Content.Title, sw.Content.UpdatedAt })
                    .Select(g => new TopTrendingArticleDto
                    {
                        Id = g.Key.ContentId,
                        Title = g.Key.Title,
                        CumulativeWeight = g.Sum(sw => sw.PopularityWeight), // Il feedback reale di apprezzamento
                        UpdatedAt = g.Key.UpdatedAt
                    })
                    .OrderByDescending(a => a.CumulativeWeight)
                    .Take(3)
                    .ToListAsync();

                // 4. Gli Argomenti (Tag) che vanno maggiormente sul sito
                viewModel.TopTags = await _context.StatisticalWeights
                    .GroupBy(sw => new { sw.TagId, sw.Tag.Name, sw.Tag.CategoryGroup })
                    .Select(g => new TopTagDto
                    {
                        TagId = g.Key.TagId,
                        TagName = g.Key.Name,
                        Category = g.Key.CategoryGroup,
                        TotalGlobalWeight = g.Sum(sw => sw.PopularityWeight)
                    })
                    .OrderByDescending(t => t.TotalGlobalWeight)
                    .Take(5)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                // Logga l'errore se necessario
            }

            // Passiamo il modello popolato alla vista parziale
            return Partial("_GeneralDashboardPartial", viewModel);
        }

        // Handler per la vista "Lista Articoli"
        public async Task<PartialViewResult> OnGetArticlesListAsync()
        {
            var viewModel = new ArticlesManagementViewModel();

            try
            {
                viewModel.Articles = await _context.Contents
                    .Include(c => c.Author) // <--- Forza il caricamento dell'utente!
                    .Include(c => c.ContentTags)
                    .ThenInclude(ct => ct.Tag)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new ArticleRowDto
                    {
                        Id = c.Id,
                        Title = c.Title,
                        PublishState = c.PublishState,
                        CreatedAt = c.CreatedAt,
                        UpdatedAt = c.UpdatedAt,
                        AuthorName = c.Author != null ? c.Author.UserName : "Vanitas Staff",
                        Category = c.ContentTags
                            .Select(ct => ct.Tag != null ? ct.Tag.CategoryGroup : "Senza Tag")
                            .FirstOrDefault() ?? "Senza Tag",
                        EliminatedAt = c.EliminatedAt
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                // Logga l'errore se necessario
            }
            return Partial("_ArticlesListPartial", viewModel);
        }

        // Handler per la vista "Gestione Tag"
        public async Task<PartialViewResult> OnGetTagsManagementAsync()
        {
            var viewModel = new TagsManagementViewModel();

            try
            {
                // 1. Popoliamo la lista completa con i sinonimi inclusi
                viewModel.TagsList = await _context.Tags
                    .Include(t => t.Synonyms)
                    .OrderBy(t => t.CategoryGroup)
                    .ThenBy(t => t.Name)
                    .Select(t => new TagManagementRowDto
                    {
                        Id = t.Id,
                        Name = t.Name,
                        CategoryGroup = t.CategoryGroup,
                        Synonyms = t.Synonyms.Select(s => s.SynonymName).ToList() // Sostituisci .Word con il nome della colonna stringa nel tuo TagSynonym
                    })
                    .ToListAsync();

                // 2. Popoliamo la lista d'appoggio per la select del Form
                viewModel.AvailableTags = await _context.Tags
                    .OrderBy(t => t.Name)
                    .Select(t => new AvailableTagDto
                    {
                        Id = t.Id,
                        Name = t.Name
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return Partial("_TagsManagementPartial", viewModel);
        }

        // Assicurati che _userManager sia iniettato nel costruttore della pagina Admin
        public async Task<PartialViewResult> OnGetStaffManagementAsync()
        {
            var viewModel = new StaffManagementViewModel();

            try
            {
                // 1. Prendiamo i dati base dal DB (Query leggera)
                var rawUsers = await _context.Users
                    .Select(u => new {
                        u.Id,
                        u.UserName,
                        ArticlesCount = u.AuthoredArticles.Count
                    })
                    .ToListAsync();

                // 2. Mappiamo il ViewModel integrando i Ruoli reali di ASP.NET Identity
                foreach (var rawUser in rawUsers)
                {
                    // Cerchiamo l'utente reale per usare i metodi di Identity
                    var fullUser = await _userManager.FindByIdAsync(rawUser.Id.ToString());
                    string mappedRole = "Utente Base";

                    if (fullUser != null)
                    {
                        var roles = await _userManager.GetRolesAsync(fullUser);
                        if (roles.Contains("Admin")) mappedRole = "Admin";
                        else if (roles.Contains("Editor") || fullUser.ReceivedPromotions.Any()) mappedRole = "Editor";
                    }

                    viewModel.StaffMembers.Add(new UserRowDto
                    {
                        Id = rawUser.Id,
                        Username = rawUser.UserName ?? "Anonimo",
                        ArticlesWritten = rawUser.ArticlesCount,
                        Role = mappedRole
                    });
                }

                // 3. Recuperiamo i Log (Invariato)
                viewModel.RecentLogs = await _context.AdminLogs
                    .Include(l => l.User)
                    .OrderByDescending(l => l.ExecutedAt)
                    .Take(20)
                    .Select(l => new AdminLogRowDto
                    {
                        Id = l.Id,
                        OperatorUsername = l.User.UserName ?? "Sistema",
                        ActionType = l.ActionType,
                        Description = l.Description,
                        ExecutedAt = l.ExecutedAt
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StaffManagement Error]: {ex.Message}");
            }

            return Partial("_StaffManagementPartial", viewModel);
        }

        public async Task<PartialViewResult> OnGetAkinatorAnalyticsAsync()
        {
            var viewModel = new AkinatorAnalyticsViewModel();

            try
            {
                // 1. Calcolo delle metriche aggregate
                viewModel.TotalSearches = await _context.SearchHistories.CountAsync();

                int successfulCount = await _context.SearchHistories.CountAsync(s => s.IsSuccessful);
                viewModel.SuccessRate = viewModel.TotalSearches > 0
                    ? Math.Round((double)successfulCount / viewModel.TotalSearches * 100, 1)
                    : 0;

                viewModel.TotalBounces = viewModel.TotalSearches - successfulCount;

                // 2. Ultime 15 sessioni dell'Akinator
                viewModel.RecentQueries = await _context.SearchHistories
                    .Include(s => s.User)
                    .Include(s => s.ResultContent)
                    .OrderByDescending(s => s.Timestamp)
                    .Take(15)
                    .Select(s => new SearchHistoryRowDto
                    {
                        Id = s.Id,
                        Username = s.User.UserName ?? "Ospite",
                        QueryTags = s.QueryTags,
                        MatchedContentTitle = s.ResultContent != null ? s.ResultContent.Title : null,
                        IsSuccessful = s.IsSuccessful,
                        Timestamp = s.Timestamp
                    })
                    .ToListAsync();

                // 3. Classifica dei "Termini Fantasma" più cercati (Ricerche fallite raggruppate)
                // Ipotizziamo che QueryTags contenga la stringa digitata in caso di fallimento
                viewModel.TopGhostTerms = await _context.SearchHistories
                    .Where(s => !s.IsSuccessful)
                    .GroupBy(s => s.QueryTags)
                    .Select(g => new GhostTermDto
                    {
                        Term = g.Key,
                        SearchCount = g.Count(),
                        LastSearched = g.Max(s => s.Timestamp)
                    })
                    .OrderByDescending(g => g.SearchCount)
                    .Take(10)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AkinatorAnalytics Error]: {ex.Message}");
            }

            return Partial("_AkinatorAnalyticsPartial", viewModel);
        }

        // POST: Applica il Soft-Delete senza rimuovere l'articolo dalle query generali
        public async Task<JsonResult> OnPostDeleteArticleAsync([FromBody] DeleteArticleRequest request)
        {
            try
            {
                // Se il model binder ha funzionato, request non è null e request.Id è valorizzato
                if (request == null || request.Id <= 0)
                {
                    return new JsonResult(new { success = false, message = "Errore di mapping: ID non ricevuto correttamente nel Body." });
                }

                // Recuperiamo l'ID reale dall'oggetto della richiesta
                int id = request.Id;

                // Cerchiamo l'articolo nel database
                var article = await _context.Contents.FindAsync(id);

                if (article == null)
                {
                    return new JsonResult(new { success = false, message = $"Articolo con ID #{id} non trovato nel database." });
                }

                // Applichiamo il Soft-Delete
                article.EliminatedAt = DateTime.UtcNow;

                // Logga l'azione nella scatola nera
                var currentUserId = _userManager.GetUserId(User);
                if (!string.IsNullOrEmpty(currentUserId))
                {
                    _context.AdminLogs.Add(new AdminLog
                    {
                        UserId = int.Parse(currentUserId),
                        ActionType = "Eliminazione",
                        Description = $"Spostato nel cestino l'articolo ID #{article.Id}: '{article.Title}'",
                        ExecutedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();

                return new JsonResult(new
                {
                    success = true,
                    message = $"L'articolo '{article.Title}' è stato spostato nel Cestino."
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Errore server: {ex.Message}" });
            }
        }
    }
}
