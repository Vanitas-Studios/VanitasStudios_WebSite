using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VanitasStudios_WebApp.Data;
using VanitasStudios_WebApp.Models;

namespace VanitasStudios_WebApp.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ApplicationDbContext _context;

        public IndexModel(ILogger<IndexModel> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IList<Content> PinnedArticles { get; set; } = new List<Content>();

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Carichiamo i contenuti in evidenza, filtrando per lo stato Pubblico 
                // Carichiamo anche le Sections in modalità Eager Loading (.Include) per generare l'estratto se la descrizione manca
                PinnedArticles = await _context.Contents
                    .Include(c => c.Sections)
                    .Where(c => c.IsPinned && c.PublishState == PublishState.Pubblico)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(4) // Limite di sicurezza per preservare l'equilibrio del layout
                    .ToListAsync();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dei frammenti in evidenza dal database.");
                PinnedArticles = new List<Content>();
                return Page();
            }
        }
    }
}
