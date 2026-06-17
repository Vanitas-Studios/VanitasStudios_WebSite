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
                .Include(c => c.ContentTags)
                .FirstOrDefaultAsync(c => c.Id == id);

            if(Content == null)
            {
                return NotFound();
            }

            //Sections = await _context.Sections
            //    .Include(s => s.Images)
            //    .Include(s => s.Videos)
            //    .Where(s => s.ContentId == id)
            //    .OrderBy(s => s.Order)
            //    .ToListAsync();

            //var tagIds = Content.ContentTags.Select(t => t.IdT).ToList();

            //RelatedContents = await _context.Contents
            //    .Where(c => c.Id != id) 
            //    .Where(c => c.ContentTags.Any(t => tagIds.Contains(t.IdT))) 
            //    .OrderByDescending(c => c.CreatedAt)
            //    .Take(3) 
            //    .ToListAsync();


            return Page();
        }

    }
}
