using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using VanitasStudios_WebApp.Data;
using VanitasStudios_WebApp.Models;

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
            public string Title { get; set; }
            public List<SectionViewModel>? Sections { get; set; }
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

        public class PreviewRequest
        {
            public int ArticleId { get; set; }
        }

        public class TagActionDto
        {
            public int ArticleId { get; set; }
            public int TagId { get; set; }
        }
        public class StatusActionDto
        {
            public int ArticleId { get; set; }
            public string Action { get; set; } = null!;
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
                CurrentContent = await _context.Contents
                                    .Include(c => c.Sections)
                                    .FirstOrDefaultAsync(m => m.Id == id);
                if (CurrentContent == null) return NotFound();

                // La data è quella che leggiamo dal DB
                LastModified = (DateTime)CurrentContent.UpdatedAt;
                ArticleId = id;
            }
            else
            {
                // NUOVO CONTENUTO
                // 1. Recuperiamo l'ID dell'utente loggato direttamente dai Claims (arriva come stringa, es: "1")
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userIdString))
                {
                    return RedirectToPage("/Account/Login");
                }

                // 2. Convertiamo la stringa in un intero nativo (visto che ApplicationUser usa <int>)
                int currentUserId = int.Parse(userIdString);

                // 3. Cerchiamo l'autore direttamente tramite la sua Chiave Primaria (Id)
                var currentAuthor = await _context.Users.FindAsync(currentUserId);

                if (currentAuthor == null)
                {
                    return NotFound("Autore non trovato nel sistema con questo ID.");
                }

                CurrentContent = new Content
                {
                    Title = "Nuovo Articolo",
                    Slug = "nuovo-articolo-" + Guid.NewGuid().ToString().Substring(0, 5), // Evita slug duplicati all'inizio
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    PublishState = PublishState.Bozza,
                    AuthorId = currentAuthor.Id
                };

                LastModified = DateTime.UtcNow;
                _context.Contents.Add(CurrentContent);
                await _context.SaveChangesAsync(); // Qui il DB genera l'ID

                // Ora reindirizziamo alla stessa pagina ma con l'ID appena creato
                // Questo evita che l'utente crei mille articoli vuoti premendo F5
                return RedirectToPage(new { id = CurrentContent.Id });
            }

            // Inserisci questo pezzetto di codice temporaneo dentro l'OnGet o OnGetAsync della pagina
            if (!_context.Tags.Any())
            {
                var tagDiTest = new List<Tag>
                    {
                        new Tag { Name = "Calisthenics", CategoryGroup = "Allenamento" },
                        new Tag { Name = "Programmazione", CategoryGroup = "Tech" },
                        new Tag { Name = "C#", CategoryGroup = "Tech" },
                        new Tag { Name = "Game Development", CategoryGroup = "Design" },
                        new Tag { Name = "Unity", CategoryGroup = "Design" },
                        new Tag { Name = "Minimalism", CategoryGroup = "Art" },
                        new Tag { Name = "Dark Fantasy", CategoryGroup = "Scrittura" },
                        new Tag { Name = "Web Design", CategoryGroup = "Tech" }
                    };

                _context.Tags.AddRange(tagDiTest);
                await _context.SaveChangesAsync();
            }

            return Page();
        }

        //public async Task<IActionResult> OnPostSaveContentAsync([FromBody] EditorSavePayload payload)
        //{
        //   // Controllo di base: Validazione 
        //   if(payload == null || payload.ArticleId == 0)
        //    {
        //        return new JsonResult(new { success = false, message = "Invalid Data" });
        //    }

        //    // SICUREZZA: Se il JS manda la proprietà vuota o con un nome disallineato, 
        //    // evitiamo il crash inizializzandola come lista vuota.
        //    payload.Sections ??= new List<SectionViewModel>();

        //    // Controlliamo che il contenuto esista e preleviamo le sezioni esistenti per aggiornarle.
        //    var article = await _context.Contents
        //                    .Include(c => c.Sections)
        //                    .FirstOrDefaultAsync(i => i.Id == payload.ArticleId);

        //    if (article == null) return new JsonResult(new { success = false, message = "Article not Found" });

        //    var incomingIds = payload.Sections
        //                        .Where(i => !i.Id.StartsWith("temp-"))
        //                        .Select(s => int.Parse(s.Id))
        //                        .ToList();

        //    var sectionsToRemove = article.Sections
        //                            .Where(i => !incomingIds.Contains(i.Id))
        //                            .ToList();
        //    if (sectionsToRemove.Any())
        //    {
        //        _context.Sections.RemoveRange(sectionsToRemove);
        //    }

        //    foreach( var sDto in payload.Sections)
        //    {
        //        if (sDto.Id.StartsWith("temp-")) continue;

        //        if(int.TryParse(sDto.Id, out int realId))
        //        {
        //            var existingSections = article.Sections.FirstOrDefault(s => s.Id == realId);

        //            if (existingSections != null)
        //            {
        //                // TODO: Implementare HtmlSanitizer per pulire sDto.Content
        //                string cleanContent = sDto.Content
        //                    .Replace("\u200B", "").Trim();

        //                existingSections.HtmlText = cleanContent;
        //                existingSections.Title = sDto.Title?.Trim();
        //                existingSections.Order = sDto.Order;
        //            }
        //        }
        //    }

        //    article.UpdatedAt = DateTime.UtcNow;

        //    try
        //    {
        //        await _context.SaveChangesAsync();
        //        return new JsonResult(new { success = true, lastUpdate = article.UpdatedAt });
        //    }
        //    catch (DbUpdateException ex)
        //    {
        //        // Logga l'errore per Vanitas Studios
        //        return new JsonResult(new { success = false, message = "Errore durante il salvataggio nel database" });
        //    }
        //}




        //public async Task<IActionResult> OnPostSaveSectionAsync([FromBody] SectionViewModel sDto)
        //{
        //    if (sDto == null) return new JsonResult(new { success = false });

        //    Section section;
        //    bool isNew = sDto.Id.StartsWith("temp-");

        //    if (isNew)
        //    {
        //        // L'utente ha appena premuto invio: creiamo la sezione "vuota"
        //        section = new Section
        //        {
        //            ContentId = sDto.ArticleId,
        //            Title = sDto.Title?.Trim() ?? "Senza Titolo",
        //            HtmlText = sDto.Content ?? "", // Sarà probabilmente stringa vuota all'inizio
        //            Order = sDto.Order
        //        };
        //        _context.Sections.Add(section);
        //    }
        //    else
        //    {
        //        // Aggiornamento di una sezione esistente (già dotata di ID)
        //        if (!int.TryParse(sDto.Id, out int realId)) return BadRequest();

        //        section = await _context.Sections.FindAsync(realId);
        //        if (section == null) return NotFound();

        //        // Aggiorniamo solo se i dati sono effettivamente diversi (ottimizzazione)
        //        section.Title = sDto.Title?.Trim() ?? section.Title;
        //        section.Order = sDto.Order;

        //        // Se sDto.Content è null (magari non lo invii per risparmiare banda), 
        //        // non sovrascrivere il testo esistente.
        //        if (sDto.Content != null)
        //        {
        //            section.HtmlText = sDto.Content.Replace("\u200B", "").Trim();
        //        }
        //    }

        //    await _context.SaveChangesAsync();

        //    // Aggiorniamo il timestamp dell'articolo per mostrare "Ultima modifica: poco fa"
        //    var article = await _context.Contents.FindAsync(sDto.ArticleId);
        //    if (article != null)
        //    {
        //        article.UpdatedAt = DateTime.UtcNow;
        //        await _context.SaveChangesAsync();
        //    }

        //    return new JsonResult(new { success = true, sectionId = section.Id });
        //}

        public async Task<IActionResult> OnPostSaveContentAsync([FromBody] EditorSavePayload payload)
        {
            Debug.WriteLine("=== [DEBUG VANITAS] ENTRATO IN ONPOSTSAVECONTENTASYNC OPTIMIZED ===");
            if (payload == null)
            {
                return new JsonResult(new { success = false, message = "C# Errore: Payload globale nullo." }); // [cite: 53]
            }

            if (payload.Sections == null)
            {
                payload.Sections = new List<SectionViewModel>(); // [cite: 54-55]
            }

            try
            {
                var article = await _context.Contents
                                        .Include(c => c.Sections)
                                        .FirstOrDefaultAsync(i => i.Id == payload.ArticleId); // [cite: 56]
                if (article == null)
                {
                    return new JsonResult(new { success = false, message = "Article not Found" }); // [cite: 58]
                }

                // 1. Aggiornamento Titolo Principale dell'Articolo
                if (!string.IsNullOrWhiteSpace(payload.Title))
                {
                    article.Title = payload.Title.Trim(); // [cite: 58]
                }
                else if (string.IsNullOrWhiteSpace(article.Title))
                {
                    article.Title = "Nuovo articolo"; // [cite: 59]
                }

                if (!payload.Sections.Any())
                {
                    article.UpdatedAt = DateTime.UtcNow; // [cite: 60]
                    await _context.SaveChangesAsync(); // [cite: 60]
                    return new JsonResult(new { success = true, lastUpdate = article.UpdatedAt, message = "Solo timestamp aggiornato." }); // [cite: 60]
                }

                // 2. Rilevazione ed Eliminazione Sezioni Rimosse (Sincronizzazione)
                var incomingIds = payload.Sections
                                    .Where(i => i.Id != null && !i.Id.StartsWith("temp-"))
                                    .Select(s => int.Parse(s.Id))
                                    .ToList(); // [cite: 61]

                var sectionsToRemove = article.Sections
                                            .Where(i => !incomingIds.Contains(i.Id))
                                            .ToList(); // [cite: 62]
                if (sectionsToRemove.Any())
                {
                    _context.Sections.RemoveRange(sectionsToRemove); // [cite: 62]
                }

                // 3. Loop di Aggiornamento / Inserimento dei blocchi Sezione
                foreach (var sDto in payload.Sections)
                {
                    // Ignoriamo i blocchi temporanei in questa fase (vengono salvati dal salvataggio singolo OnPostSaveSectionAsync)
                    if (sDto.Id == null || sDto.Id.StartsWith("temp-")) continue; // [cite: 63]

                    if (int.TryParse(sDto.Id, out int realId)) // [cite: 64]
                    {
                        var existingSection = article.Sections.FirstOrDefault(s => s.Id == realId); // [cite: 64]
                        if (existingSection != null)
                        {
                            // Pulizia finale lato server contro caratteri invisibili (es: Zero-Width Space \u200B)
                            string cleanContent = sDto.Content?.Replace("\u200B", "").Trim() ?? ""; // [cite: 65]

                            // Ottimizzazione: Assegniamo i dati puliti
                            existingSection.HtmlText = cleanContent; // 
                            existingSection.Title = !string.IsNullOrWhiteSpace(sDto.Title) ? sDto.Title.Trim() : "Senza Titolo"; // 
                            existingSection.Order = sDto.Order; // 
                        }
                    }
                }

                article.UpdatedAt = DateTime.UtcNow; // 
                await _context.SaveChangesAsync(); // 

                Debug.WriteLine("=== [DEBUG VANITAS] SALVATAGGIO GLOBALE COMPLETATO CON SUCCESSO ==="); // [cite: 67]
                return new JsonResult(new { success = true, lastUpdate = article.UpdatedAt }); // [cite: 67]
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CRASH GLOBAL SAVE]: {ex.Message}"); // [cite: 68]
                return new JsonResult(new { success = false, error = "Crash globale", details = ex.Message }); // [cite: 68]
            }
        }

        public async Task<IActionResult> OnPostSaveSectionAsync([FromBody] SectionViewModel sDto)
        {
            Debug.WriteLine("=== [DEBUG VANITAS] ENTRATO IN ONPOSTSAVESECTIONASYNC (SINGOLO) ===");

            if (sDto == null)
            {
                Debug.WriteLine("[ERR] DTO Sezione singola NULLO");
                return new JsonResult(new { success = false, message = "C# Errore: DTO Singolo nullo." });
            }

            Debug.WriteLine($"[INFO] Sezione Singola - Id: {sDto.Id}, ArticleId: {sDto.ArticleId}, Ordine: {sDto.Order}");

            try
            {
                Section section;
                bool isNew = string.IsNullOrEmpty(sDto.Id) || sDto.Id.StartsWith("temp-");

                if (isNew)
                {
                    Debug.WriteLine($"[INFO] Rilevata NUOVA sezione. Controllo esistenza articolo ID: {sDto.ArticleId}");
                    var contentExists = await _context.Contents.AnyAsync(c => c.Id == sDto.ArticleId);
                    if (!contentExists)
                    {
                        Debug.WriteLine($"[ERR] Impossibile creare sezione: l'articolo {sDto.ArticleId} non esiste.");
                        return new JsonResult(new { success = false, message = $"L'articolo con ID {sDto.ArticleId} non esiste!" });
                    }

                    section = new Section
                    {
                        ContentId = sDto.ArticleId,
                        Title = sDto.Title?.Trim() ?? "Senza Titolo",
                        HtmlText = sDto.Content ?? "",
                        Order = sDto.Order
                    };
                    _context.Sections.Add(section);
                }
                else
                {
                    if (!int.TryParse(sDto.Id, out int realId))
                    {
                        Debug.WriteLine($"[ERR] ID Sezione esistente non parsabile: {sDto.Id}");
                        return new JsonResult(new { success = false, message = "ID non valido" });
                    }

                    section = await _context.Sections.FindAsync(realId);
                    if (section == null)
                    {
                        Debug.WriteLine($"[ERR] Sezione {realId} non trovata nel DB");
                        return new JsonResult(new { success = false, message = "Sezione non trovata" });
                    }

                    section.Title = sDto.Title?.Trim() ?? section.Title;
                    section.Order = sDto.Order;
                    if (sDto.Content != null)
                    {
                        section.HtmlText = sDto.Content.Replace("\u200B", "").Trim();
                    }
                }

                await _context.SaveChangesAsync();

                var article = await _context.Contents.FindAsync(sDto.ArticleId);
                if (article != null)
                {
                    article.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                Debug.WriteLine($"=== [DEBUG VANITAS] SEZIONE SINGOLA SALVATA CON ID REALE: {section.Id} ===");
                return new JsonResult(new { success = true, sectionId = section.Id });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CRASH SINGLE SAVE]: {ex.Message}");
                return new JsonResult(new { success = false, error = "Crash singolo", details = ex.Message, inner = ex.InnerException?.Message });
            }
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

        public async Task<IActionResult> OnPostUploadMediaAsync([FromForm] IFormFile file, [FromForm] int articleId, [FromForm] string uploadType, [FromForm] int sectionId)
        {
            // 1. Validazioni Generiche (Tuo codice nativo ottimizzato)
            if (file == null || file.Length == 0) return BadRequest(new { messaggio = "File vuoto" });

            string[] fileExtensions = [".png", ".jpg", ".jpeg", ".webp", ".mp4"];
            string currentExtension = Path.GetExtension(file.FileName).ToLower();
            if (!fileExtensions.Contains(currentExtension)) return BadRequest(new { messaggio = "Formato non supportato" });

            if (file.Length > 5 * 1024 * 1024) return BadRequest(new { messaggio = "File troppo pesante" });

            // Verifichiamo l'articolo
            var article = await _context.Contents.FirstOrDefaultAsync(c => c.Id == articleId);
            if (article == null) return NotFound(new { messaggio = "Articolo non trovato" });

            string contentType = currentExtension == ".mp4" ? "video" : "image";

            // 2. Gestione Dinamica della Cartella in base all'uploadType
            // Se è "cover" va in image/covers, altrimenti segue il percorso standard per data
            string subPath = uploadType.ToLower() == "cover"
                ? Path.Combine("image", "covers")//? Path.Combine(contentType, "covers")
                : GenerateFolderPath(contentType, file.FileName);

            string? baseroot = _config["ExternalAssetsPath"];
            string fullPath = Path.Combine(baseroot, subPath);

            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);

            // 3. Hash e Nome File
            string hashName = GenerateImageHashName(file.FileName + file.Length.ToString());
            string finalName = $"{hashName}{currentExtension}";
            string physicalSavePath = Path.Combine(fullPath, finalName);

            //string publicUrl = $"/media/{subPath.Replace("\\", "/")}/{finalName}";
            string webSubPath = subPath.Replace("\\", "/");
            string publicUrl = $"/media/{webSubPath}/{finalName}";

            // Generazione Alt Text
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName);
            string[] values = fileNameWithoutExt.Split(new[] { '/', '-', '_', '|', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string imageAlt = string.Join(" ", values);

            // 4. Salvataggio Fisico (se non esiste già)
            if (!System.IO.File.Exists(physicalSavePath))
            {
                using (var stream = new FileStream(physicalSavePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
            }

            // 5. AGGIORNAMENTO LOGICA DB (Il cuore del cambio)
            if (uploadType.ToLower() == "cover")
            {
                // Caso A: È la copertina dell'articolo
                article.CoverImageUrl = publicUrl;
                article.UpdatedAt = DateTime.UtcNow;
                _context.Contents.Update(article);
            }
            else
            {
                // Caso B: È un'immagine di sezione. La tracciamo nella tabella Media.
                // Calcoliamo l'ordine di visualizzazione all'interno di QUELLA specifica sezione
                int currentCountInSection = await _context.Media
                    .Where(m => m.SectionId == sectionId)
                    .CountAsync();

                var nuovoMedia = new Media
                {
                    Url = publicUrl,
                    Caption = imageAlt,
                    Type = MediaType.Image,
                    SectionId = sectionId, // ID della sezione reale passato dal JS
                    Order = currentCountInSection + 1 // Contatore progressivo interno
                };

                await _context.Media.AddAsync(nuovoMedia);
            }

            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                url = publicUrl,
                alt = imageAlt,
                extension = currentExtension,
                uploadType = uploadType
            });
        }

        //public async Task<IActionResult> OnPostUploadMediaAsync([FromForm] IFormFile file, [FromForm] int ArticleId)
        //{

        //    if (file == null || file.Length == 0)
        //    {
        //        return BadRequest(new { messaggio = "File vuoto" });
        //    }

        //    string[] fileExtensions = [".png", ".jpg", ".jpeg", ".webp", ".mp4"];
        //    string currentExtension = Path.GetExtension(file.FileName).ToLower(); //Fix --> controllare mime type o magic bytes

        //    if(!fileExtensions.Contains(currentExtension))
        //    {
        //        return BadRequest(new {messaggio = "Formato non supportato"});
        //    }

        //    string contentType = currentExtension switch
        //    {
        //        ".mp4" => "video",
        //        _ => "image"
        //    };

        //    if (file.Length > 5 * 1024 * 1024)
        //    {
        //        return BadRequest(new { messaggio = "File troppo pesante" });
        //    }

        //    string? baseroot = _config["ExternalAssetsPath"];
        //    string subPath = GenerateFolderPath(contentType, file.FileName);
        //    string fullPath = Path.Combine(baseroot, subPath);

        //    if (!Directory.Exists(fullPath))
        //    {
        //        Directory.CreateDirectory(fullPath);
        //    }

        //    string hashName = GenerateImageHashName(file.FileName + file.Length.ToString());
        //    string finalName = $"{hashName}{currentExtension}";
        //    string physicalSavePath = Path.Combine(fullPath, finalName);

        //    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName); 
        //    string[] values = fileNameWithoutExt.Split(new[] { '/', '-', '_', '|', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        //    string imageAlt = string.Join(" ", values);

        //    string publicUrl = $"/media/{subPath.Replace("\\", "/")}/{finalName}";

        //    if (System.IO.File.Exists(physicalSavePath))
        //    {
        //        // Se esiste già, non serve salvarlo di nuovo, restituiamo direttamente l'URL
        //        return new JsonResult(new { url = publicUrl, alt = imageAlt, extension = currentExtension, success = true });
        //    }

        //    using (var stream = new FileStream(physicalSavePath, FileMode.Create))
        //    {
        //        await file.CopyToAsync(stream);
        //    }

        //    return new JsonResult(new
        //    {
        //        url = publicUrl,
        //        alt = imageAlt,
        //        extension = currentExtension,
        //        success = true
        //    });

        //}

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

        public async Task<IActionResult> OnPostLoadPreviewAsync([FromBody] PreviewRequest request)
        {
            if (request == null || request.ArticleId == 0)
            {
                return new JsonResult(new { success = false, message = "Payload non valido." });
            }

            int articleId = request.ArticleId;

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

        public async Task<IActionResult> OnGetSearchTagsAsync(string query, int articleId)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new JsonResult(new List<object>());
            }

            string cleanQuery = query.Trim().ToLower();

            // 1. Prendiamo gli ID dei tag già associati a questo articolo per escluderli
            var excludedTagIds = await _context.ContentTags
                .Where(ct => ct.ContentId == articleId)
                .Select(ct => ct.TagId)
                .ToListAsync();

            // 2. Cerchiamo i tag corrispondenti escludendo i duplicati
            var tags = await _context.Tags
                .Where(t => t.Name.ToLower().Contains(cleanQuery) && !excludedTagIds.Contains(t.Id))
                .OrderBy(t => t.Name)
                .Take(10) // Limite massimo di efficienza
                .Select(t => new { id = t.Id, name = t.Name }) // Payload leggerissimo
                .ToListAsync();

            return new JsonResult(tags);
        }

        // 2. HANDLER: Aggiunta associazione Tag
        [HttpPost]
        public async Task<IActionResult> OnPostAddTagAsync([FromBody] TagActionDto data)
        {
            if (data == null || data.ArticleId <= 0 || data.TagId <= 0)
            {
                return new JsonResult(new { success = false, message = "Dati della richiesta non validi." });
            }

            try
            {
                // 1. Verifichiamo se l'associazione esiste già
                bool alreadyExists = await _context.ContentTags
                    .AnyAsync(ct => ct.ContentId == data.ArticleId && ct.TagId == data.TagId);

                if (alreadyExists)
                {
                    return new JsonResult(new { success = true, message = "Tag già associato." });
                }

                // 2. Creiamo l'oggetto valorizzando TUTTI i campi richiesti, incluso il Weight
                var newContentTag = new ContentTag
                {
                    ContentId = data.ArticleId,
                    TagId = data.TagId,
                    Weight = 1.0f // Impostiamo il peso iniziale (fondamentale per l'albero di decisione dell'IA)
                };

                // 3. Passiamo la variabile corretta al metodo Add
                _context.ContentTags.Add(newContentTag);
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                // Se si pianta ancora, puoi mettere un breakpoint qui per leggere ex.InnerException
                return new JsonResult(new { success = false, message = "Errore durante il salvataggio sul database." });
            }
        }

        // 3. HANDLER: Rimozione associazione Tag
        public async Task<IActionResult> OnPostRemoveTagAsync([FromBody] TagActionDto data)
        {
            if (data == null || data.ArticleId <= 0 || data.TagId <= 0)
            {
                return new JsonResult(new { success = false, message = "Dati della richiesta non validi." });
            }

            try
            {
                // Cerchiamo la riga specifica nella tabella di giunzione
                var contentTagToRemove = await _context.ContentTags
                    .FirstOrDefaultAsync(ct => ct.ContentId == data.ArticleId && ct.TagId == data.TagId);

                if (contentTagToRemove == null)
                {
                    // Se non c'è già, per il client è comunque un successo (idempotenza)
                    return new JsonResult(new { success = true });
                }

                _context.ContentTags.Remove(contentTagToRemove);
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = "Errore durante la rimozione dal database." });
            }
        }

        public async Task<IActionResult> OnPostChangeStatusAsync([FromBody] StatusActionDto data)
        {
            if (data == null || data.ArticleId <= 0 || string.IsNullOrEmpty(data.Action))
            {
                return new JsonResult(new { success = false, message = "Dati della richiesta non validi." });
            }

            try
            {
                // CONTROLLO DI SICUREZZA 1: Esistenza dell'articolo
                var article = await _context.Contents
                    .FirstOrDefaultAsync(c => c.Id == data.ArticleId);

                if (article == null)
                {
                    return new JsonResult(new { success = false, message = "Articolo non trovato." });
                }

                // CONTROLLO DI SICUREZZA 2: Integrità del contenuto prima della pubblicazione
                // Evitiamo che venga messo online un articolo senza titolo o palesemente incompleto
                if ((data.Action == "Publish" || data.Action == "Update") && string.IsNullOrWhiteSpace(article.Title))
                {
                    return new JsonResult(new { success = false, message = "Impossibile pubblicare un articolo senza titolo." });
                }

                // 3. GESTIONE DELLE AZIONI LOGICHE
                switch (data.Action)
                {
                    case "Publish":
                        // Passa da Bozza a Pubblicato
                        article.PublishState = PublishState.Pubblico;

                        // Impostiamo la data di pubblicazione solo se non è mai stato pubblicato prima
                        if (article.UpdatedAt == null)
                        {
                            article.UpdatedAt = DateTime.UtcNow; // O DateTime.Now a seconda di come gestisci i fusi orari
                        }
                        break;

                    case "Update":
                        // Se è già pubblicato, l'azione di aggiornamento conferma lo stato
                        // e aggiorna la data di ultima modifica (se hai quel campo)
                        article.PublishState = PublishState.Pubblico;
                        article.UpdatedAt = DateTime.UtcNow;
                        break;

                    case "ToDraft":
                        // Riporta l'articolo in bozza (oscurandolo dal frontend pubblico)
                        article.PublishState = PublishState.Bozza;
                        break;

                    default:
                        return new JsonResult(new { success = false, message = "Azione non riconosciuta." });
                }

                // 4. SALVATAGGIO FINALE
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                // Qui puoi loggare l'errore specifico (es. ex.Message)
                return new JsonResult(new { success = false, message = "Errore critico durante l'aggiornamento dello stato." });
            }
        }

    }
}
