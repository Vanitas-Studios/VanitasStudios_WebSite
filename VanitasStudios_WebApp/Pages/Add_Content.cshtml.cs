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
using System.Text.RegularExpressions;
using VanitasStudios_WebApp.Data;
using VanitasStudios_WebApp.Models;

namespace VanitasStudios_WebApp.Pages
{
    [Authorize(Roles = "Admin,Editor")]
    public class Add_ContentModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public int? ArticleId { get; set; }
        public DateTime LastModified { get; set; }

        [BindProperty]
        public List<Tag> AvailableTags { get; set; } = new();

        public Content CurrentContent { get; set; }

        [BindProperty]
        public List<Content> Suggested { get; set; } = new();

        [BindProperty]
        public Dictionary<string, int> OrderIndex { get; set; } = new();

        [BindProperty]
        public List<Section> SectionsList { get; set; } = new();

        // ==========================================
        // DTO & PAYLOADS (Invariati per il JS)
        // ==========================================
        public class EditorSavePayload
        {
            public int ArticleId { get; set; }
            public string Title { get; set; } = null!;
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
            public List<string> SortedIds { get; set; } = new();
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

        // ==========================================
        // HANDLER CORE: ON GET
        // ==========================================
        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id.HasValue)
            {
                // Caricamento articolo esistente con sezioni incluse
                CurrentContent = await _context.Contents
                    .Include(c => c.Sections)
                    .Include(c => c.ContentTags)
                        .ThenInclude(ct => ct.Tag)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (CurrentContent == null) return NotFound();

                LastModified = CurrentContent.UpdatedAt ?? DateTime.UtcNow;
                ArticleId = id;
            }
            else
            {
                // Inizializzazione Nuovo Contenuto in Sicurezza
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdString))
                {
                    return RedirectToPage("/Account/Login");
                }

                int currentUserId = int.Parse(userIdString);
                var currentAuthor = await _context.Users.FindAsync(currentUserId);
                if (currentAuthor == null)
                {
                    return NotFound("Autore non trovato nel sistema con questo ID.");
                }

                CurrentContent = new Content
                {
                    Title = "Nuovo Articolo",
                    Slug = "nuovo-articolo-" + Guid.NewGuid().ToString()[..5],
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    PublishState = PublishState.Bozza,
                    AuthorId = currentAuthor.Id
                };

                LastModified = DateTime.UtcNow;
                _context.Contents.Add(CurrentContent);
                await _context.SaveChangesAsync();

                // Post-Redirect-Get per impedire doppie creazioni con F5
                return RedirectToPage(new { id = CurrentContent.Id });
            }

            // Seed dei tag di test se il database è vuoto
            if (!await _context.Tags.AnyAsync())
            {
                var tagDiTest = new List<Tag>
                {
                    new() { Name = "Calisthenics", CategoryGroup = "Allenamento" },
                    new() { Name = "Programmazione", CategoryGroup = "Tech" },
                    new() { Name = "C#", CategoryGroup = "Tech" },
                    new() { Name = "Game Development", CategoryGroup = "Design" },
                    new() { Name = "Unity", CategoryGroup = "Design" },
                    new() { Name = "Minimalism", CategoryGroup = "Art" },
                    new() { Name = "Dark Fantasy", CategoryGroup = "Scrittura" },
                    new() { Name = "Web Design", CategoryGroup = "Tech" }
                };
                _context.Tags.AddRange(tagDiTest);
                await _context.SaveChangesAsync();
            }

            return Page();
        }

        // ==========================================
        // HANDLER AJAX: SALVATAGGIO GLOBALE OPTIMIZED
        // ==========================================
        public async Task<IActionResult> OnPostSaveContentAsync([FromBody] EditorSavePayload payload)
        {
            Debug.WriteLine("=== [DEBUG VANITAS] ENTRATO IN ONPOSTSAVECONTENTASYNC OPTIMIZED ===");
            if (payload == null)
            {
                return new JsonResult(new { success = false, message = "C# Errore: Payload globale nullo." });
            }

            payload.Sections ??= new List<SectionViewModel>();

            try
            {
                var article = await _context.Contents
                    .Include(c => c.Sections)
                    .FirstOrDefaultAsync(i => i.Id == payload.ArticleId);

                if (article == null)
                {
                    return new JsonResult(new { success = false, message = "Articolo non trovato." });
                }

                // 1. Gestione Titolo & SEO Slug
                if (!string.IsNullOrWhiteSpace(payload.Title))
                {
                    article.Title = payload.Title.Trim();
                    if (article.PublishState == PublishState.Bozza)
                    {
                        article.Slug = GenerateSlug(article.Title);
                    }
                }
                else if (string.IsNullOrWhiteSpace(article.Title))
                {
                    article.Title = "Nuovo articolo";
                }

                if (!payload.Sections.Any())
                {
                    article.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return new JsonResult(new { success = true, lastUpdate = article.UpdatedAt, message = "Solo timestamp aggiornato." });
                }

                // 2. Sincronizzazione ed Eliminazione sezioni rimosse dal layout grafico
                var incomingIds = payload.Sections
                    .Where(i => i.Id != null && !i.Id.StartsWith("temp-"))
                    .Select(s => int.Parse(s.Id!))
                    .ToList();

                var sectionsToRemove = article.Sections
                    .Where(i => !incomingIds.Contains(i.Id))
                    .ToList();

                if (sectionsToRemove.Any())
                {
                    _context.Sections.RemoveRange(sectionsToRemove);
                }

                // 3. Update dei blocchi di testo storici
                foreach (var sDto in payload.Sections)
                {
                    if (sDto.Id == null || sDto.Id.StartsWith("temp-")) continue;

                    if (int.TryParse(sDto.Id, out int realId))
                    {
                        var existingSection = article.Sections.FirstOrDefault(s => s.Id == realId);
                        if (existingSection != null)
                        {
                            string cleanContent = sDto.Content?.Replace("\u200B", "").Trim() ?? "";
                            existingSection.HtmlText = cleanContent;
                            existingSection.Title = !string.IsNullOrWhiteSpace(sDto.Title) ? sDto.Title.Trim() : "Senza Titolo";
                            existingSection.Order = sDto.Order;
                        }
                    }
                }

                // 4. Estrattore Intelligente della Descrizione SEO per frammenti
                var firstTextSection = article.Sections
                    .Where(s => !string.IsNullOrWhiteSpace(s.HtmlText))
                    .MinBy(s => s.Order);

                if (firstTextSection != null)
                {
                    string plainText = Regex.Replace(firstTextSection.HtmlText, "<.*?>", string.Empty);
                    plainText = Regex.Replace(plainText, @"\s+", " ").Trim();

                    int maxChars = 160;
                    if (plainText.Length > maxChars)
                    {
                        string truncated = plainText[..maxChars];
                        int lastSpace = truncated.LastIndexOf(' ');
                        article.Description = lastSpace > 0 ? truncated[..lastSpace] + "..." : truncated + "...";
                    }
                    else
                    {
                        article.Description = plainText;
                    }
                }
                else
                {
                    article.Description = "Fragments of an untold chronicle. Read the full entry to uncover its structure.";
                }

                article.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                Debug.WriteLine("=== [DEBUG VANITAS] SALVATAGGIO GLOBALE COMPLETATO CON SUCCESSO ===");
                return new JsonResult(new { success = true, lastUpdate = article.UpdatedAt });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CRASH GLOBAL SAVE]: {ex.Message}");
                return new JsonResult(new { success = false, error = "Crash globale del server", details = ex.Message });
            }
        }

        // ==========================================
        // HANDLER AJAX: SALVATAGGIO BLOCCO SINGOLO (INVIO)
        // ==========================================
        public async Task<IActionResult> OnPostSaveSectionAsync([FromBody] SectionViewModel sDto)
        {
            if (sDto == null)
            {
                return new JsonResult(new { success = false, message = "C# Errore: DTO Singolo nullo." });
            }

            try
            {
                Section section;
                bool isNew = string.IsNullOrEmpty(sDto.Id) || sDto.Id.StartsWith("temp-");

                if (isNew)
                {
                    var contentExists = await _context.Contents.AnyAsync(c => c.Id == sDto.ArticleId);
                    if (!contentExists)
                    {
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
                        return new JsonResult(new { success = false, message = "ID sezione non valido." });
                    }

                    section = await _context.Sections.FindAsync(realId);
                    if (section == null)
                    {
                        return new JsonResult(new { success = false, message = "Sezione non trovata nel database." });
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

                return new JsonResult(new { success = true, sectionId = section.Id });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, error = "Crash salvataggio singolo", details = ex.Message });
            }
        }

        // ==========================================
        // HANDLER AJAX: ELIMINAZIONE BLOCCO SEZIONE
        // ==========================================
        public async Task<IActionResult> OnPostDeleteSectionAsync(DeleteSectionDto dto)
        {
            var section = await _context.Sections
                .FirstOrDefaultAsync(s => s.Id == dto.SectionId && s.ContentId == dto.ArticleId);

            if (section == null)
                return new JsonResult(new { success = true, message = "Sezione già rimossa o inesistente." });

            int eliminatedOrder = section.Order;
            _context.Sections.Remove(section);

            // Ricalibrazione degli indici di ordinamento successivi per evitare buchi
            var nextSections = await _context.Sections
                .Where(s => s.ContentId == dto.ArticleId && s.Order > eliminatedOrder)
                .ToListAsync();

            foreach (var s in nextSections)
            {
                s.Order--;
            }

            var article = await _context.Contents.FindAsync(dto.ArticleId);
            if (article != null)
            {
                article.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true, message = "Sezione eliminata e indici ricalcolati." });
        }

        // ==========================================
        // HANDLER AJAX: DRAG & DROP REORDER RECALC
        // ==========================================
        public async Task<IActionResult> OnPostUpdateOrderAsync([FromBody] NewOrder sectionOrder)
        {
            if (sectionOrder?.SortedIds == null) return BadRequest();

            var articleExists = await _context.Contents.AnyAsync(c => c.Id == sectionOrder.ArticleId);
            if (!articleExists)
            {
                return new JsonResult(new { success = false, message = "Articolo non trovato." });
            }

            var articleSections = await _context.Sections
                .Where(s => s.ContentId == sectionOrder.ArticleId)
                .ToListAsync();

            for (int i = 0; i < sectionOrder.SortedIds.Count; i++)
            {
                string badgeId = sectionOrder.SortedIds[i];
                var section = articleSections.FirstOrDefault(s => s.Id.ToString() == badgeId);
                if (section != null)
                {
                    section.Order = i + 1; // 1-based index
                }
            }

            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }

        // ==========================================
        // HANDLER UPLOAD FILE: COVER E MEDIA INTERNI
        // ==========================================
        public async Task<IActionResult> OnPostUploadMediaAsync([FromForm] IFormFile file, [FromForm] int articleId, [FromForm] string uploadType, [FromForm] int sectionId)
        {
            if (file == null || file.Length == 0) return BadRequest(new { messaggio = "File vuoto o non valido" });

            string[] fileExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".mp4" };
            string currentExtension = Path.GetExtension(file.FileName).ToLower();

            if (!fileExtensions.Contains(currentExtension)) return BadRequest(new { messaggio = "Estensione file non supportata." });
            if (file.Length > 5 * 1024 * 1024) return BadRequest(new { messaggio = "File troppo pesante. Massimo 5MB." });

            var article = await _context.Contents.FirstOrDefaultAsync(c => c.Id == articleId);
            if (article == null) return NotFound(new { messaggio = "Articolo non trovato" });

            string contentType = currentExtension == ".mp4" ? "video" : "image";
            string subPath = uploadType.ToLower() == "cover"
                ? Path.Combine("image", "covers")
                : GenerateFolderPath(contentType, file.FileName);

            string? baseroot = _config["ExternalAssetsPath"];
            if (string.IsNullOrEmpty(baseroot))
            {
                return StatusCode(500, new { messaggio = "Configurazione ExternalAssetsPath mancante sul server." });
            }

            string fullPath = Path.Combine(baseroot, subPath);
            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);

            string hashName = GenerateImageHashName(file.FileName + file.Length);
            string finalName = $"{hashName}{currentExtension}";
            string physicalSavePath = Path.Combine(fullPath, finalName);

            string webSubPath = subPath.Replace("\\", "/");
            string publicUrl = $"/media/{webSubPath}/{finalName}";

            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName);
            string[] values = fileNameWithoutExt.Split(new[] { '/', '-', '_', '|', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string imageAlt = string.Join(" ", values);

            // Salvataggio effettivo su disco se l'hash univoco non è presente
            if (!System.IO.File.Exists(physicalSavePath))
            {
                await using var stream = new FileStream(physicalSavePath, FileMode.Create);
                await file.CopyToAsync(stream);
            }

            if (uploadType.ToLower() == "cover")
            {
                article.CoverImageUrl = publicUrl;
                article.UpdatedAt = DateTime.UtcNow;
                _context.Contents.Update(article);
            }
            else
            {
                int currentCountInSection = await _context.Media.CountAsync(m => m.SectionId == sectionId);
                var nuovoMedia = new Media
                {
                    Url = publicUrl,
                    Caption = imageAlt,
                    Type = MediaType.Image,
                    SectionId = sectionId,
                    Order = currentCountInSection + 1
                };
                await _context.Media.AddAsync(nuovoMedia);
            }

            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true, url = publicUrl, alt = imageAlt, extension = currentExtension, uploadType });
        }

        // ==========================================
        // HANDLER AJAX: GENERAZIONE LIVE PREVIEW
        // ==========================================
        public async Task<IActionResult> OnPostLoadPreviewAsync([FromBody] PreviewRequest request)
        {
            if (request == null || request.ArticleId == 0)
            {
                return new JsonResult(new { success = false, message = "Payload non valido." });
            }

            var currentArticle = await _context.Contents.FirstOrDefaultAsync(i => i.Id == request.ArticleId);
            if (currentArticle == null)
            {
                return new JsonResult(new { success = false, message = "Articolo non trovato." });
            }

            var sectionsList = await _context.Sections
                .Where(s => s.ContentId == request.ArticleId)
                .OrderBy(s => s.Order)
                .ToListAsync();

            if (!sectionsList.Any())
            {
                return new JsonResult(new { success = false, message = "Nessuna sezione trovata." });
            }

            var htmlBuilder = new StringBuilder();
            var orderIndex = new List<object>();

            htmlBuilder.AppendLine("<html><head><title>Page-Preview</title>");
            htmlBuilder.AppendLine("</head><body class='vanitas-preview-mode'><div class='container-fluid main-section'><div class='row'>");
            htmlBuilder.AppendLine("    <div class='col-md-3 suggested-article'><h5 class='text-muted'>Correlati IA</h5></div>");
            htmlBuilder.AppendLine("    <div class='col-md-6 article-body'>");
            htmlBuilder.AppendLine($"       <h1 class='display-4'>{currentArticle.Title}</h1><hr />");

            foreach (var s in sectionsList)
            {
                htmlBuilder.AppendLine($"<div id='section-anchor-{s.Id}' class='mb-4'>{s.HtmlText}</div>");
                orderIndex.Add(new { title = s.Title ?? "Sezione senza titolo", order = s.Order, id = s.Id });
            }

            htmlBuilder.AppendLine("    </div><div class='col-md-3 content-index'><div class='sticky-top' style='top: 20px;'><h5>Indice Contenuti</h5><ul class='list-unstyled'>");

            foreach (var s in sectionsList)
            {
                string displayTitle = s.Title ?? $"Sezione {s.Order}";
                htmlBuilder.AppendLine($"<li class='mb-2'><a href='#section-anchor-{s.Id}' class='text-decoration-none'>{displayTitle}</a></li>");
            }

            htmlBuilder.AppendLine("        </ul></div></div></div></div></body></html>");

            return new JsonResult(new { success = true, htmlContent = htmlBuilder.ToString(), index = orderIndex });
        }

        // ==========================================
        // UTILITIES & TAG CONTROLLERS
        // ==========================================
        public async Task<IActionResult> OnGetSearchTagsAsync(string query, int articleId)
        {
            if (string.IsNullOrWhiteSpace(query)) return new JsonResult(new List<object>());

            string cleanQuery = query.Trim().ToLower();
            var excludedTagIds = await _context.ContentTags
                .Where(ct => ct.ContentId == articleId)
                .Select(ct => ct.TagId)
                .ToListAsync();

            var tags = await _context.Tags
                .Where(t => t.Name.ToLower().Contains(cleanQuery) && !excludedTagIds.Contains(t.Id))
                .OrderBy(t => t.Name)
                .Take(10)
                .Select(t => new { id = t.Id, name = t.Name })
                .ToListAsync();

            return new JsonResult(tags);
        }

        public async Task<IActionResult> OnPostAddTagAsync([FromBody] TagActionDto data)
        {
            if (data == null || data.ArticleId <= 0 || data.TagId <= 0)
            {
                return new JsonResult(new { success = false, message = "Dati richiesta non validi." });
            }

            try
            {
                bool alreadyExists = await _context.ContentTags.AnyAsync(ct => ct.ContentId == data.ArticleId && ct.TagId == data.TagId);
                if (alreadyExists) return new JsonResult(new { success = true, message = "Tag già associato." });

                var newContentTag = new ContentTag { ContentId = data.ArticleId, TagId = data.TagId, Weight = 1.0f };
                _context.ContentTags.Add(newContentTag);
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true });
            }
            catch
            {
                return new JsonResult(new { success = false, message = "Errore durante il salvataggio sul database." });
            }
        }

        public async Task<IActionResult> OnPostRemoveTagAsync([FromBody] TagActionDto data)
        {
            if (data == null || data.ArticleId <= 0 || data.TagId <= 0)
            {
                return new JsonResult(new { success = false, message = "Dati richiesta non validi." });
            }

            try
            {
                var contentTagToRemove = await _context.ContentTags.FirstOrDefaultAsync(ct => ct.ContentId == data.ArticleId && ct.TagId == data.TagId);
                if (contentTagToRemove != null)
                {
                    _context.ContentTags.Remove(contentTagToRemove);
                    await _context.SaveChangesAsync();
                }
                return new JsonResult(new { success = true });
            }
            catch
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
                var article = await _context.Contents.FirstOrDefaultAsync(c => c.Id == data.ArticleId);
                if (article == null) return new JsonResult(new { success = false, message = "Articolo non trovato." });

                if ((data.Action == "Publish" || data.Action == "Update") && string.IsNullOrWhiteSpace(article.Title))
                {
                    return new JsonResult(new { success = false, message = "Impossibile pubblicare un articolo senza titolo." });
                }

                switch (data.Action)
                {
                    case "Publish" or "Update":
                        article.PublishState = PublishState.Pubblico;
                        article.UpdatedAt = DateTime.UtcNow;
                        break;
                    case "ToDraft":
                        article.PublishState = PublishState.Bozza;
                        break;
                    default:
                        return new JsonResult(new { success = false, message = "Azione non riconosciuta." });
                }

                await _context.SaveChangesAsync();
                return new JsonResult(new { success = true });
            }
            catch
            {
                return new JsonResult(new { success = false, message = "Errore critico durante l'aggiornamento dello stato." });
            }
        }

        public string GenerateFolderPath(string category, string fileName)
        {
            string categoryPath = category.ToLower();
            string year = DateTime.UtcNow.ToString("yyyy");
            string monthDay = DateTime.UtcNow.ToString("MM_dd");
            return Path.Combine(categoryPath, year, monthDay);
        }

        public string GenerateImageHashName(string imageBit)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(imageBit);
            byte[] hashBytes = MD5.HashData(inputBytes); // Ottimizzazione moderna .NET senza istanziare MD5.Create()

            StringBuilder sb = new();
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private string GenerateSlug(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "untitled";
            string str = title.ToLowerInvariant().Trim();
            str = Regex.Replace(str, @"[\s_]+", "-");
            str = Regex.Replace(str, @"[^a-z0-9\-]", "");
            str = Regex.Replace(str, @"-+", "-");
            return str.Trim('-');
        }
    }
}

