using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using VanitasStudios_WebApp.Data;
using VanitasStudios_WebApp.Models;

namespace VanitasStudios_WebApp.Pages
{
    public class GameDocModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public List<Content> Contents { get; set; }
        public List<Tag> Tags { get; set; } 
        [BindProperty(SupportsGet = true)]
        public List<int> SelectedTagIds { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchQuery { get; set; } 

        public GameDocModel(ApplicationDbContext dbContext)
        {
            _context = dbContext;
        }
        public async Task OnGetAsync()
        {
            var query = _context.Contents
                .Include(c => c.TagOrds)
                .Where(c => c.TypeC == "documentation")
                .OrderByDescending(c => c.DataPub)
                .AsQueryable();

            if (SelectedTagIds != null && SelectedTagIds.Count > 0)
            {
                query = query.Where(c => c.TagOrds.Any(t => SelectedTagIds.Contains(t.IdT)));
            }
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                query = query.Where(c => c.Title.Contains(SearchQuery) || c.DescC.Contains(SearchQuery));
            }

            Contents = await query
                .AsNoTracking()
                .ToListAsync();
            Tags = Contents.SelectMany(c => c.TagOrds)
                .Distinct()
                .ToList();
        }
    }
}
