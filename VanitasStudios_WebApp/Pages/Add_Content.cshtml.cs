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
            public List<SectionViewModel>? SectionList { get; set; }
        }

        public class SectionViewModel
        {
            public int ArticleId { get; set; }
            public string? Id { get; set; }
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
            public List<string>? SortedIds { get; set; }
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
                LastModified = (DateTime)CurrentContent.UpdatedAt;
                ArticleId = id;
            }
            else
            {
                // NUOVO CONTENUTO
                CurrentContent = new Content
                {
                    Title = "Nuovo Articolo",
                    // Inizializziamo la data al momento della creazione
                    UpdatedAt = DateTime.UtcNow
                };

                LastModified = DateTime.UtcNow;
                _context.Contents.Add(CurrentContent);
                await _context.SaveChangesAsync(); // Qui il DB genera l'ID

                // Ora reindirizziamo alla stessa pagina ma con l'ID appena creato
                // Questo evita che l'utente crei mille articoli vuoti premendo F5
                return RedirectToPage(new { id = CurrentContent.Id });
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
                            .FirstOrDefaultAsync(i => i.Id == payload.ArticleId);

            if (article == null) return new JsonResult(new { success = false, message = "Article not Found" });

            var incomingIds = payload.SectionList
                                .Where(i => !i.Id.StartsWith("temp-"))
                                .Select(s => int.Parse(s.Id))
                                .ToList();

            var sectionsToRemove = article.Sections
                                    .Where(i => !incomingIds.Contains(i.Id))
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
                    var existingSections = article.Sections.FirstOrDefault(s => s.Id == realId);

                    if (existingSections != null)
                    {
                        // TODO: Implementare HtmlSanitizer per pulire sDto.Content
                        string cleanContent = sDto.Content
                            .Replace("\u200B", "").Trim();

                        existingSections.HtmlText = cleanContent;
                        existingSections.Title = sDto.Title?.Trim();
                        existingSections.Order = sDto.Order;
                    }
                }
            }

            article.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
                return new JsonResult(new { success = true, lastUpdate = article.UpdatedAt });
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
                    ContentId = sDto.ArticleId,
                    Title = sDto.Title?.Trim() ?? "Senza Titolo",
                    HtmlText = sDto.Content ?? "", // Sarà probabilmente stringa vuota all'inizio
                    Order = sDto.Order
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
                section.Order = sDto.Order;

                // Se sDto.Content è null (magari non lo invii per risparmiare banda), 
                // non sovrascrivere il testo esistente.
                if (sDto.Content != null)
                {
                    section.HtmlText = sDto.Content.Replace("\u200B", "").Trim();
                }
            }

            await _context.SaveChangesAsync();

            // Aggiorniamo il timestamp dell'articolo per mostrare "Ultima modifica: poco fa"
            var article = await _context.Contents.FindAsync(sDto.ArticleId);
            if (article != null)
            {
                article.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return new JsonResult(new { success = true, sectionId = section.Id });
        }

        public async Task<IActionResult> OnPostDeleteSectionAsync(DeleteSectionDto dto)
        {
            // 1. Recupera la sezione controllando la proprietà di navigazione (o FK) dell'articolo
            Section section = await _context.Sections
                .FirstOrDefaultAsync(s => s.Id == dto.SectionId && s.ContentId == dto.ArticleId);

            // Se non esiste, rispondiamo comunque success: true (idempotenza: se era già cancellata, il risultato desiderato è ottenuto)
            if (section == null)
                return new JsonResult(new { success = true, message = "Section already deleted or not found" });

            int eliminatedOrder = section.Order;
            _context.Sections.Remove(section);

            // 2. Recupera le sezioni successive per scalare l'ordine
            var nextSections = await _context.Sections
                .Where(s => s.ContentId == dto.ArticleId && s.Order > eliminatedOrder)
                .ToListAsync();

            // Se ci sono sezioni successive, scala il loro indice di 1
            if (nextSections.Any())
            {
                foreach (var s in nextSections)
                {
                    s.Order--;
                }
            }

            // 3. Aggiorna la data di modifica dell'articolo padre (Coerenza temporale)
            var article = await _context.Contents.FindAsync(dto.ArticleId);
            if (article != null)
            {
                article.UpdatedAt = DateTime.UtcNow; // Usiamo sempre UtcNow come stabilito!
            }

            // 4. Unico salvataggio per ottimizzare le transazioni sul DB
            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true, message = "Section deleted and structural order synchronized" });
        }


        public async Task<IActionResult> OnPostUpdateOrderAsync([FromBody] NewOrder sectionOrder)
        {
            // 1. Usiamo l'await corretto per verificare se l'articolo esiste
            var articleExists = await _context.Contents.AnyAsync(c => c.Id == sectionOrder.ArticleId);
            if (!articleExists)
            {
                return new JsonResult(new { success = false, message = "Articolo non trovato." });
            }

            // 2. Recuperiamo TUTTE le sezioni di questo articolo con UNA SOLA query
            var articleSections = await _context.Sections
                .Where(s => s.ContentId == sectionOrder.ArticleId)
                .ToListAsync();

            // 3. Cicliamo l'array ricevuto dal frontend
            for (int i = 0; i < sectionOrder.SortedIds.Count; i++)
            {
                string badgeId = sectionOrder.SortedIds[i];

                // Cerchiamo la sezione nella lista in memoria (molto più veloce, non tocca il DB)
                var section = articleSections.FirstOrDefault(s => s.Id.ToString() == badgeId);
                if (section != null)
                {
                    section.Order = i + 1; // Aggiorna l'ordine (1-based)
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
        [BindProperty]
        public List<Content> Suggested {  get; set; }
        [BindProperty]
        public Dictionary<string,int> OrderIndex { get; set; } // l'indice potrebbe semplicemente associare al titolo, l'ordine...?
        [BindProperty]
        public List<Section> SectionsList { get; set; }

        public async Task<IActionResult> OnPostLoadPreviewAsync([FromBody] int articleId)
        {
            // Controllo preventivo sull'articolo per evitare NullReferenceException
            var currentArticle = await _context.Contents.FirstOrDefaultAsync(i => i.Id == articleId);
            if (currentArticle == null)
            {
                return new JsonResult(new { success = false, message = "Articolo non trovato." });
            }

            // 1. Recuperiamo le sezioni ORDINATE direttamente dal database
            var sectionsList = await _context.Sections
                                            .Where(s => s.ContentId == articleId)
                                            .OrderBy(s => s.Order)
                                            .ToListAsync();

            if (sectionsList == null || !sectionsList.Any())
            {
                return new JsonResult(new { success = false, message = "Nessuna sezione trovata per questo articolo." });
            }

            var htmlBuilder = new StringBuilder();
            var orderIndex = new List<object>();

            // Costruzione Head e Body dell'Iframe
            htmlBuilder.AppendLine("<html>");
            htmlBuilder.AppendLine("<head>");
            htmlBuilder.AppendLine("  <title>Page-Preview</title>");
            // Quando sarai pronto, scommenta queste righe per applicare i tuoi stili reali!
            // htmlBuilder.AppendLine("  <link rel='stylesheet' href='/css/bootstrap.min.css'>");
            // htmlBuilder.AppendLine("  <link rel='stylesheet' href='/css/vanitas-theme.css'>");
            htmlBuilder.AppendLine("</head>");
            htmlBuilder.AppendLine("<body class='vanitas-preview-mode'>");
            htmlBuilder.AppendLine("<div class='container-fluid main-section'>");
            htmlBuilder.AppendLine("  <div class='row'>");

            // COLONNA SINISTRA: Spazio IA (In futuro dinamico)
            htmlBuilder.AppendLine("    <div class='col-md-3 suggested-article'><h5 class='text-muted'>Correlati IA</h5></div>");

            // COLONNA CENTRALE: Corpo del documento
            htmlBuilder.AppendLine("    <div class='col-md-6 article-body'>");
            htmlBuilder.AppendLine($"       <h1 class='display-4'>{currentArticle.Title}</h1>");
            htmlBuilder.AppendLine("        <hr />");

            // Ciclo 1: Stampiamo il corpo dell'articolo e popoliamo la lista per il JSON
            foreach (var s in sectionsList)
            {
                // Avvolgiamo la sezione in un div con un ID univoco per permettere l'ancoraggio dello scroll
                htmlBuilder.AppendLine($"<div id='section-anchor-{s.Id}' class='mb-4'>");
                htmlBuilder.AppendLine(s.HtmlText);
                htmlBuilder.AppendLine("</div>");

                orderIndex.Add(new
                {
                    title = s.Title ?? "Sezione senza titolo",
                    order = s.Order,
                    id = s.Id
                });
            }
            htmlBuilder.AppendLine("    </div>");

            // COLONNA DESTRA: Generazione dell'INDICE DINAMICO
            htmlBuilder.AppendLine("    <div class='col-md-3 content-index'>");
            htmlBuilder.AppendLine("      <div class='sticky-top' style='top: 20px;'>"); // Mantiene l'indice fermo durante lo scroll
            htmlBuilder.AppendLine("        <h5>Indice Contenuti</h5>");
            htmlBuilder.AppendLine("        <ul class='list-unstyled'>");

            // Ciclo 2: Generiamo i link puntatori reali per l'indice dentro l'iframe
            foreach (var s in sectionsList)
            {
                string displayTitle = s.Title ?? $"Sezione {s.Order}";
                // Il link punta all'ID del div generato nel Ciclo 1
                htmlBuilder.AppendLine($"<li class='mb-2'><a href='#section-anchor-{s.Id}' class='text-decoration-none'>{displayTitle}</a></li>");
            }

            htmlBuilder.AppendLine("        </ul>");
            htmlBuilder.AppendLine("      </div>");
            htmlBuilder.AppendLine("    </div>");

            // Chiusura dei tag HTML
            htmlBuilder.AppendLine("  </div>");
            htmlBuilder.AppendLine("</div>");
            htmlBuilder.AppendLine("</body>");
            htmlBuilder.AppendLine("</html>");

            return new JsonResult(new
            {
                success = true,
                htmlContent = htmlBuilder.ToString(),
                index = orderIndex
            });
        }

    }
}
