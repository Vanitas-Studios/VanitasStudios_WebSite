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
                            .FirstOrDefault() ?? "Senza Tag"
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                // Logga l'errore se necessario
            }
            return Partial("_ArticlesListPartial");
        }

        // Handler per la vista "Gestione Tag"
        public PartialViewResult OnGetTagsManagement()
        {
            return Partial("_TagsManagementPartial");
        }


    }
}
