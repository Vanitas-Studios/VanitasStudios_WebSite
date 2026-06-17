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

        public class DashboardViewModel
        {
            public int TotalArticlesOnline { get; set; }
            public int TotalArticlesDraft { get; set; }
            public int TotalViews28Days { get; set; }
            public string AverageReadingTime { get; set; } = "0m 0s";

            // Lista dei 3 articoli più visti
            public List<TopArticleDto> TopArticles { get; set; } = new();
        }

        public class TopArticleDto
        {
            public int Id { get; set; }
            public string Title { get; set; } = null!;
            public int Views { get; set; }
            public DateTime? PublishedAt { get; set; }
            public string TagsInline { get; set; } = "";
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

                // 2. Calcolo metriche fittizie o reali (es. se hai una colonna Views)
                viewModel.TotalViews28Days = await _context.Contents
                    .Where(c => c.PublishState == PublishState.Pubblico)
                    .SumAsync(c => c.Views); // Assumendo che tu abbia un campo Views

                viewModel.AverageReadingTime = "3m 45s"; // Gestibile in futuro con logiche di analytics

                // 3. Query per i 3 articoli più performanti con i loro Tag
                viewModel.TopArticles = await _context.Contents
                    .Where(c => c.PublishState == PublishState.Pubblico)
                    .OrderByDescending(c => c.Views)
                    .Take(3)
                    .Select(c => new TopArticleDto
                    {
                        Id = c.Id,
                        Title = c.Title,
                        Views = c.Views,
                        PublishedAt = c.PublishedAt,
                        // Uniamo i tag in una stringa singola per la visualizzazione rapida
                        TagsInline = string.Join(", ", c.ContentTags.Select(ct => ct.Tag.Name))
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                // Logga l'errore se necessario
                // Il viewModel vuoto eviterà comunque il crash della pagina
            }

            // Passiamo il modello popolato alla vista parziale
            return Partial("_GeneralDashboardPartial", viewModel);
        }

        // Handler per la vista "Lista Articoli"
        public PartialViewResult OnGetArticlesList()
        {
            return Partial("_ArticlesListPartial");
        }

        // Handler per la vista "Gestione Tag"
        public PartialViewResult OnGetTagsManagement()
        {
            return Partial("_TagsManagementPartial");
        }


    }
}
