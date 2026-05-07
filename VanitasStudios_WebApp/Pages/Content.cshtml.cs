using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VanitasStudios_WebApp.Data;
using VanitasStudios_WebApp.Models;

namespace VanitasStudios_WebApp.Pages
{
    public class ContentModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public Content? Content { get; set; }
        public List<Content> RelatedContents { get; set; }
        public List<Section>? Sections { get; set; }

        public ContentModel(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> OnGetAsync(int id)
        {
            Content = await _context.Contents
                .Include(c => c.TagOrds)
                .FirstOrDefaultAsync(c => c.IdC == id);

            if(Content == null)
            {
                return NotFound();
            }

            Sections = await _context.Sections
                .Include(s => s.Images)
                .Include(s => s.Videos)
                .Where(s => s.ContentSId == id)
                .OrderBy(s => s.OrderNum)
                .ToListAsync();

            var tagIds = Content.TagOrds.Select(t => t.IdT).ToList();

            RelatedContents = await _context.Contents
                .Where(c => c.IdC != id) 
                .Where(c => c.TagOrds.Any(t => tagIds.Contains(t.IdT))) 
                .OrderByDescending(c => c.DataPub)
                .Take(3) 
                .ToListAsync();


            return Page();
        }
    }
}
