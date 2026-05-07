using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VanitasStudios_WebApp.Data;
using VanitasStudios_WebApp.Models;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace VanitasStudios_WebApp.Pages
{
    [Authorize(Roles = "Admin,Editor")]
    public class Add_ContentModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _context;
        [BindProperty]
        public List<Tag> AvailableTags { get; set; } 
        public Content NewContent { get; set; }
        [BindProperty]
        public SectionViewModel Section { get; set; }

        public class SectionViewModel
        {
            public int Id { get; set; }
            public string? Title { get; set; }
            public string? Content { get; set; }
            public int Order { get; set; }
            public string[]? TagList { get; set; }
        }

        public Add_ContentModel(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }
        public async Task OnGetAsync()
        {
            AvailableTags = await _context.Tags
                            .OrderBy(t => t.TagName)
                            .AsNoTracking()
                            .ToListAsync();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSaveContentAsync(string publish, string Title, string Content)
        {
            bool isPublishing = publish == "true";

            bool hasIntro = Content.Contains("##Introduzione");
            bool hasEnd = Content.Contains("##Conclusione");

            List<string> SectionsList = Content.Split(new string[] { "##" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            
            

            if(isPublishing && (hasIntro || hasEnd))
            {
                TempData["Success"] = "Pubblicazione ruscita: reinvio alla pagina della revisione.";
                return RedirectToAction("Editor");
            }

            return Page();
        }
        public async Task<IActionResult> OnPostUploadMediaAsync([FromForm] IFormFile file)
        {

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { messaggio = "File vuoto" });
            }

            string[] fileExtensions = [".png", ".jpg", ".jpeg", ".webp", ".mp4"];
            string currentExtension = Path.GetExtension(file.FileName).ToLower(); //Fix --> controllare mime type o magic bytes

            if(!fileExtensions.Contains(currentExtension))
            {
                return BadRequest(new {messaggio = "Formato non supportato"});
            }

            string contentType = currentExtension switch
            {
                ".mp4" => "video",
                _ => "image"
            };

            if (file.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { messaggio = "File troppo pesante" });
            }

            string? baseroot = _config["ExternalAssetsPath"];
            string subPath = GenerateFolderPath(contentType, file.FileName);
            string fullPath = Path.Combine(baseroot, subPath);

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            string hashName = GenerateImageHashName(file.FileName + file.Length.ToString());
            string finalName = $"{hashName}{currentExtension}";
            string physicalSavePath = Path.Combine(fullPath, finalName);

            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName); 
            string[] values = fileNameWithoutExt.Split(new[] { '/', '-', '_', '|', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string imageAlt = string.Join(" ", values);

            string publicUrl = $"/media/{subPath.Replace("\\", "/")}/{finalName}";

            if (System.IO.File.Exists(physicalSavePath))
            {
                // Se esiste già, non serve salvarlo di nuovo, restituiamo direttamente l'URL
                return new JsonResult(new { url = publicUrl, alt = imageAlt, extension = currentExtension, success = true });
            }

            using (var stream = new FileStream(physicalSavePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return new JsonResult(new
            {
                url = publicUrl,
                alt = imageAlt,
                extension = currentExtension,
                success = true
            });

        }

        public string GenerateFolderPath(string category, string fileName)
        {
            string categoryPath = category.ToLower();

            string year = DateTime.Now.ToString("yyyy");

            string monthDay = DateTime.Now.ToString("MM_dd");

            return Path.Combine(categoryPath, year, monthDay);
        }

        public string GenerateImageHashName(string imageBit)
        {
            using(MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(imageBit);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                foreach(byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }
        [IgnoreAntiforgeryToken]
        public IActionResult OnPostDeleteMedia(string fileUrl)
        {
            try
            {
                string? baseroot = _config["ExternalAssetsPath"];
                // Trasformiamo l'URL pubblico in percorso fisico
                string relativePath = fileUrl.Replace("/media/", "").Replace("/", Path.DirectorySeparatorChar.ToString());
                string fullPath = Path.Combine(baseroot, relativePath);

                if (!fullPath.StartsWith(baseroot, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Tentativo di accesso non autorizzato.");
                }

                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                    return new JsonResult(new { success = true });
                }
                return BadRequest();
            }
            catch { return StatusCode(500); }
        }
    }
}
