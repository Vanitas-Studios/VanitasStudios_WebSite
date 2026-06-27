using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VanitasStudios_WebApp.Data;
using VanitasStudios_WebApp.Models;
using VanitasStudios_WebApp.Service;

namespace VanitasStudios_WebApp.Pages
{
    public class ContentModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public Content Content { get; set; } = null!;
        public List<Content> RelatedContents { get; set; } = new();

        public ContentModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            // Carichiamo l'articolo con Autore, Tag, Sezioni e Media ordinati
            var content = await _context.Contents
                .Include(c => c.Author)
                .Include(c => c.ContentTags)
                    .ThenInclude(ct => ct.Tag)
                .Include(c => c.Sections.OrderBy(s => s.Order))
                    .ThenInclude(s => s.MediaElements.OrderBy(m => m.Order))
                .FirstOrDefaultAsync(c => c.Id == id);

            // Controllo di sicurezza: se l'articolo non esiste o è nel cestino (soft-delete), 404
            if (content == null || content.EliminatedAt != null)
            {
                return NotFound();
            }

            // Se l'articolo è una bozza, permettiamo la visualizzazione solo agli Admin o Editor per l'anteprima
            if (content.PublishState == PublishState.Bozza && !User.IsInRole("Admin") && !User.IsInRole("Editor"))
            {
                return NotFound();
            }

            Content = content;

            // Recuperiamo gli ID dei tag di questo articolo per cercare i correlati
            var tagIds = Content.ContentTags.Select(ct => ct.TagId).ToList();

            // Query Correlati: articoli pubblici, diversi da quello corrente, che condividono i tag dell'algoritmo
            RelatedContents = await _context.Contents
                .Where(c => c.Id != id && c.PublishState == PublishState.Pubblico && c.EliminatedAt == null)
                .Where(c => c.ContentTags.Any(ct => tagIds.Contains(ct.TagId)))
                .OrderByDescending(c => c.CreatedAt)
                .Take(4) // Griglia pulita di 4 elementi al massimo
                .ToListAsync();

            return Page();
        }
    }
}
