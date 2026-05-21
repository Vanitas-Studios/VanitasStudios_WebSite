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
        public int? ArticleId { get; set; } // ID del contenuto nel caso esistesse già come bozza, oppure pubblicato ma da editare.
        public DateTime LastModified { get; set; }

        // Variabili per settare configurazione per i secrets e database. 
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _context;

        [BindProperty]
        public List<Tag> AvailableTags { get; set; } 
        public Content CurrentContent { get; set; }

        public class EditorSavePayload
        {
            public int ArticleId { get; set; }
            public List<SectionViewModel> SectionList { get; set; }
        }

        public class SectionViewModel
        {
            public int ArticleId { get; set; }
            public string Id { get; set; }
            public string? Title { get; set; }
            public string? Content { get; set; }
            public int Order { get; set; }
        }

        public class DeleteSectionDto
        {
            public int SectionId { get; set; }
            public int ArticleId { get; set; }
        }
        public class NewOrder
        {
            public int ArticleId { get; set; }
            public List<string> SortedIds { get; set; }
        }

        public Add_ContentModel(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }
        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id.HasValue)
            {
                // CARICAMENTO ESISTENTE
                CurrentContent = await _context.Contents.FindAsync(id.Value);
                if (CurrentContent == null) return NotFound();

                // La data è quella che leggiamo dal DB
                LastModified = (DateTime)CurrentContent.DataEdit;
                ArticleId = id;
            }
            else
            {
                // NUOVO CONTENUTO
                CurrentContent = new Content
                {
                    Title = "Nuovo Articolo",
                    // Inizializziamo la data al momento della creazione
                    DataEdit = DateTime.UtcNow
                };

                LastModified = DateTime.UtcNow;
                _context.Contents.Add(CurrentContent);
                await _context.SaveChangesAsync(); // Qui il DB genera l'ID

                // Ora reindirizziamo alla stessa pagina ma con l'ID appena creato
                // Questo evita che l'utente crei mille articoli vuoti premendo F5
                return RedirectToPage(new { id = CurrentContent.IdC });
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveContentAsync([FromBody] EditorSavePayload payload)
        {
           // Controllo di base: Validazione 
           if(payload == null || payload.ArticleId == 0)
            {
                return new JsonResult(new { success = false, message = "Invalid Data" });
            }

           // Controlliamo che il contenuto esista e preleviamo le sezioni esistenti per aggiornarle.
           var article = await _context.Contents
                            .Include(c => c.Sections)
                            .FirstOrDefaultAsync(i => i.IdC == payload.ArticleId);

            if (article == null) return new JsonResult(new { success = false, message = "Article not Found" });

            var incomingIds = payload.SectionList
                                .Where(i => !i.Id.StartsWith("temp-"))
                                .Select(s => int.Parse(s.Id))
                                .ToList();

            var sectionsToRemove = article.Sections
                                    .Where(i => !incomingIds.Contains(i.IdS))
                                    .ToList();
            if (sectionsToRemove.Any())
            {
                _context.Sections.RemoveRange(sectionsToRemove);
            }

            foreach( var sDto in payload.SectionList)
            {
                if (sDto.Id.StartsWith("temp-")) continue;

                if(int.TryParse(sDto.Id, out int realId))
                {
                    var existingSections = article.Sections.FirstOrDefault(s => s.IdS == realId);

                    if (existingSections != null)
                    {
                        // TODO: Implementare HtmlSanitizer per pulire sDto.Content
                        string cleanContent = sDto.Content
                            .Replace("\u200B", "").Trim();

                        existingSections.SectionText = cleanContent;
                        existingSections.Title = sDto.Title?.Trim();
                        existingSections.OrderNum = sDto.Order;
                    }
                }
            }

            article.DataEdit = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
                return new JsonResult(new { success = true, lastUpdate = article.DataEdit });
            }
            catch (DbUpdateException ex)
            {
                // Logga l'errore per Vanitas Studios
                return new JsonResult(new { success = false, message = "Errore durante il salvataggio nel database" });
            }
        }


        public async Task<IActionResult> OnPostSaveSectionAsync([FromBody] SectionViewModel sDto)
        {
            if (sDto == null) return new JsonResult(new { success = false });

            Section section;
            bool isNew = sDto.Id.StartsWith("temp-");

            if (isNew)
            {
                // L'utente ha appena premuto invio: creiamo la sezione "vuota"
                section = new Section
                {
                    ContentSId = sDto.ArticleId,
                    Title = sDto.Title?.Trim() ?? "Senza Titolo",
                    SectionText = sDto.Content ?? "", // Sarà probabilmente stringa vuota all'inizio
                    OrderNum = sDto.Order
                };
                _context.Sections.Add(section);
            }
            else
            {
                // Aggiornamento di una sezione esistente (già dotata di ID)
                if (!int.TryParse(sDto.Id, out int realId)) return BadRequest();

                section = await _context.Sections.FindAsync(realId);
                if (section == null) return NotFound();

                // Aggiorniamo solo se i dati sono effettivamente diversi (ottimizzazione)
                section.Title = sDto.Title?.Trim() ?? section.Title;
                section.OrderNum = sDto.Order;

                // Se sDto.Content è null (magari non lo invii per risparmiare banda), 
                // non sovrascrivere il testo esistente.
                if (sDto.Content != null)
                {
                    section.SectionText = sDto.Content.Replace("\u200B", "").Trim();
                }
            }

            await _context.SaveChangesAsync();

            // Aggiorniamo il timestamp dell'articolo per mostrare "Ultima modifica: poco fa"
            var article = await _context.Contents.FindAsync(sDto.ArticleId);
            if (article != null)
            {
                article.DataEdit = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return new JsonResult(new { success = true, sectionId = section.IdS });
        }

        public async Task<IActionResult> OnPostDeleteSectionAsync(DeleteSectionDto dto)
        {
            // 1. Recupera la sezione controllando la proprietà di navigazione (o FK) dell'articolo
            Section section = await _context.Sections
                .FirstOrDefaultAsync(s => s.IdS == dto.SectionId && s.ContentSId == dto.ArticleId);

            // Se non esiste, rispondiamo comunque success: true (idempotenza: se era già cancellata, il risultato desiderato è ottenuto)
            if (section == null)
                return new JsonResult(new { success = true, message = "Section already deleted or not found" });

            int eliminatedOrder = section.OrderNum;
            _context.Sections.Remove(section);

            // 2. Recupera le sezioni successive per scalare l'ordine
            var nextSections = await _context.Sections
                .Where(s => s.ContentSId == dto.ArticleId && s.OrderNum > eliminatedOrder)
                .ToListAsync();

            // Se ci sono sezioni successive, scala il loro indice di 1
            if (nextSections.Any())
            {
                foreach (var s in nextSections)
                {
                    s.OrderNum--;
                }
            }

            // 3. Aggiorna la data di modifica dell'articolo padre (Coerenza temporale)
            var article = await _context.Contents.FindAsync(dto.ArticleId);
            if (article != null)
            {
                article.DataEdit = DateTime.UtcNow; // Usiamo sempre UtcNow come stabilito!
            }

            // 4. Unico salvataggio per ottimizzare le transazioni sul DB
            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true, message = "Section deleted and structural order synchronized" });
        }


        public async Task<IActionResult> OnPostUpdateOrderAsync([FromBody] NewOrder sectionOrder)
        {
            // 1. Usiamo l'await corretto per verificare se l'articolo esiste
            var articleExists = await _context.Contents.AnyAsync(c => c.IdC == sectionOrder.ArticleId);
            if (!articleExists)
            {
                return new JsonResult(new { success = false, message = "Articolo non trovato." });
            }

            // 2. Recuperiamo TUTTE le sezioni di questo articolo con UNA SOLA query
            var articleSections = await _context.Sections
                .Where(s => s.ContentSId == sectionOrder.ArticleId)
                .ToListAsync();

            // 3. Cicliamo l'array ricevuto dal frontend
            for (int i = 0; i < sectionOrder.SortedIds.Count; i++)
            {
                string badgeId = sectionOrder.SortedIds[i];

                // Cerchiamo la sezione nella lista in memoria (molto più veloce, non tocca il DB)
                var section = articleSections.FirstOrDefault(s => s.IdS.ToString() == badgeId);
                if (section != null)
                {
                    section.OrderNum = i + 1; // Aggiorna l'ordine (1-based)
                }
            }

            // 4. FONDAMENTALE: Salviamo le modifiche sul database fisicamente
            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostUploadMediaAsync([FromForm] IFormFile file, [FromForm] int ArticleId)
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

        //public IActionResult OnPostDeleteMedia(string fileUrl)
        //{
        //    try
        //    {
        //        string? baseroot = _config["ExternalAssetsPath"];
        //        // Trasformiamo l'URL pubblico in percorso fisico
        //        string relativePath = fileUrl.Replace("/media/", "").Replace("/", Path.DirectorySeparatorChar.ToString());
        //        string fullPath = Path.Combine(baseroot, relativePath);

        //        if (!fullPath.StartsWith(baseroot, StringComparison.OrdinalIgnoreCase))
        //        {
        //            return BadRequest("Tentativo di accesso non autorizzato.");
        //        }

        //        if (System.IO.File.Exists(fullPath))
        //        {
        //            System.IO.File.Delete(fullPath);
        //            return new JsonResult(new { success = true });
        //        }
        //        return BadRequest();
        //    }
        //    catch { return StatusCode(500); }
        //}
    }
}
