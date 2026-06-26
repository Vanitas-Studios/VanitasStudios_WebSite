using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using VanitasStudios_WebApp.Data;
using VanitasStudios_WebApp.Models;

namespace VanitasStudios_WebApp.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminInterfaceModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public class DeleteArticleRequest
        {
            public int Id { get; set; }
        }

        public AdminInterfaceModel(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            _context = dbContext;
            _userManager = userManager;
        }
        // Al primo caricamento restituisce la struttura fissa
        public void OnGet()
        {
        }

        // Handler per la vista "Dashboard Generale"
        public async Task<PartialViewResult> OnGetGeneralDashboardAsync()
        {
            var viewModel = new DashboardViewModel();

            try
            {
                // 1. Conteggio articoli divisi per Stato
                viewModel.TotalArticlesOnline = await _context.Contents
                    .CountAsync(c => c.PublishState == PublishState.Pubblico);

                viewModel.TotalArticlesDraft = await _context.Contents
                    .CountAsync(c => c.PublishState == PublishState.Bozza);

                // 2. Metriche dell'algoritmo Akinator dalla cronologia delle ricerche
                viewModel.TotalSearchesExecuted = await _context.SearchHistories.CountAsync();

                if (viewModel.TotalSearchesExecuted > 0)
                {
                    int successfulSearches = await _context.SearchHistories.CountAsync(sh => sh.IsSuccessful);
                    // Calcolo la percentuale di successo dell'algoritmo
                    viewModel.AlgorithmSuccessRate = Math.Round(((double)successfulSearches / viewModel.TotalSearchesExecuted) * 100, 1);
                }
                else
                {
                    viewModel.AlgorithmSuccessRate = 0.0;
                }

                // 3. Gli articoli PIÙ APPREZZATI / DI TENDENZA
                // Calcolati sommando i pesi cumulativi dei click (PopularityWeight) nella tabella pivot
                viewModel.TopArticles = await _context.StatisticalWeights
                    .Where(sw => sw.Content.PublishState == PublishState.Pubblico)
                    .GroupBy(sw => new { sw.ContentId, sw.Content.Title, sw.Content.UpdatedAt })
                    .Select(g => new TopTrendingArticleDto
                    {
                        Id = g.Key.ContentId,
                        Title = g.Key.Title,
                        CumulativeWeight = g.Sum(sw => sw.PopularityWeight), // Il feedback reale di apprezzamento
                        UpdatedAt = g.Key.UpdatedAt
                    })
                    .OrderByDescending(a => a.CumulativeWeight)
                    .Take(3)
                    .ToListAsync();

                // 4. Gli Argomenti (Tag) che vanno maggiormente sul sito
                viewModel.TopTags = await _context.StatisticalWeights
                    .GroupBy(sw => new { sw.TagId, sw.Tag.Name, sw.Tag.CategoryGroup })
                    .Select(g => new TopTagDto
                    {
                        TagId = g.Key.TagId,
                        TagName = g.Key.Name,
                        Category = g.Key.CategoryGroup,
                        TotalGlobalWeight = g.Sum(sw => sw.PopularityWeight)
                    })
                    .OrderByDescending(t => t.TotalGlobalWeight)
                    .Take(5)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                // Logga l'errore se necessario
            }

            // Passiamo il modello popolato alla vista parziale
            return Partial("_GeneralDashboardPartial", viewModel);
        }

        // Handler per la vista "Lista Articoli"
        public async Task<PartialViewResult> OnGetArticlesListAsync()
        {
            var viewModel = new ArticlesManagementViewModel();

            try
            {
                viewModel.Articles = await _context.Contents
                    .Include(c => c.Author) // <--- Forza il caricamento dell'utente!
                    .Include(c => c.ContentTags)
                    .ThenInclude(ct => ct.Tag)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new ArticleRowDto
                    {
                        Id = c.Id,
                        Title = c.Title,
                        PublishState = c.PublishState,
                        CreatedAt = c.CreatedAt,
                        UpdatedAt = c.UpdatedAt,
                        AuthorName = c.Author != null ? c.Author.UserName : "Vanitas Staff",
                        Category = c.ContentTags
                            .Select(ct => ct.Tag != null ? ct.Tag.CategoryGroup : "Senza Tag")
                            .FirstOrDefault() ?? "Senza Tag",
                        EliminatedAt = c.EliminatedAt
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                // Logga l'errore se necessario
            }
            return Partial("_ArticlesListPartial", viewModel);
        }

        // Handler per la vista "Gestione Tag"
        public async Task<PartialViewResult> OnGetTagsManagementAsync()
        {
            var viewModel = new TagsManagementViewModel();

            try
            {
                // 1. Popoliamo la lista completa con i sinonimi inclusi
                viewModel.TagsList = await _context.Tags
                    .Include(t => t.Synonyms)
                    .OrderBy(t => t.CategoryGroup)
                    .ThenBy(t => t.Name)
                    .Select(t => new TagManagementRowDto
                    {
                        Id = t.Id,
                        Name = t.Name,
                        CategoryGroup = t.CategoryGroup,
                        Synonyms = t.Synonyms.Select(s => s.SynonymName).ToList() // Sostituisci .Word con il nome della colonna stringa nel tuo TagSynonym
                    })
                    .ToListAsync();

                // 2. Popoliamo la lista d'appoggio per la select del Form
                viewModel.AvailableTags = await _context.Tags
                    .OrderBy(t => t.Name)
                    .Select(t => new AvailableTagDto
                    {
                        Id = t.Id,
                        Name = t.Name
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return Partial("_TagsManagementPartial", viewModel);
        }

        // Assicurati che _userManager sia iniettato nel costruttore della pagina Admin
        public async Task<PartialViewResult> OnGetStaffManagementAsync()
        {
            var viewModel = new StaffManagementViewModel();

            try
            {
                // 1. Prendiamo i dati base dal DB (Query leggera)
                var rawUsers = await _context.Users
                    .Select(u => new {
                        u.Id,
                        u.UserName,
                        ArticlesCount = u.AuthoredArticles.Count
                    })
                    .ToListAsync();

                // 2. Mappiamo il ViewModel integrando i Ruoli reali di ASP.NET Identity
                foreach (var rawUser in rawUsers)
                {
                    // Cerchiamo l'utente reale per usare i metodi di Identity
                    var fullUser = await _userManager.FindByIdAsync(rawUser.Id.ToString());
                    string mappedRole = "Utente Base";

                    if (fullUser != null)
                    {
                        var roles = await _userManager.GetRolesAsync(fullUser);
                        if (roles.Contains("Admin")) mappedRole = "Admin";
                        else if (roles.Contains("Editor") || fullUser.ReceivedPromotions.Any()) mappedRole = "Editor";
                    }

                    viewModel.StaffMembers.Add(new UserRowDto
                    {
                        Id = rawUser.Id,
                        Username = rawUser.UserName ?? "Anonimo",
                        ArticlesWritten = rawUser.ArticlesCount,
                        Role = mappedRole
                    });
                }

                // 3. Recuperiamo i Log (Invariato)
                viewModel.RecentLogs = await _context.AdminLogs
                    .Include(l => l.User)
                    .OrderByDescending(l => l.ExecutedAt)
                    .Take(20)
                    .Select(l => new AdminLogRowDto
                    {
                        Id = l.Id,
                        OperatorUsername = l.User.UserName ?? "Sistema",
                        ActionType = l.ActionType,
                        Description = l.Description,
                        ExecutedAt = l.ExecutedAt
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StaffManagement Error]: {ex.Message}");
            }

            return Partial("_StaffManagementPartial", viewModel);
        }

        public async Task<IActionResult> OnGetAkinatorAnalyticsAsync()
        {
            // 1. Calcolo delle metriche generali dalle cronologie di ricerca
            var totalSearches = await _context.SearchHistories.CountAsync();
            var totalBounces = await _context.SearchHistories.CountAsync(s => !s.IsSuccessful);

            double successRate = totalSearches > 0
                ? Math.Round((double)(totalSearches - totalBounces) / totalSearches * 100, 1)
                : 0;

            // 2. Query per la colonna di SINISTRA: Registro Sessioni Recenti
            var recentQueries = await _context.SearchHistories
                .Include(s => s.User)
                .Include(s => s.ResultContent)
                .OrderByDescending(s => s.Timestamp)
                .Take(15)
                .Select(s => new SearchHistoryRowDto // Sostituisci con il nome del tuo DTO/Classe interna
                {
                    Username = s.User != null ? s.User.UserName : "Ospite Anonimo",
                    Timestamp = s.Timestamp,
                    QueryTags = s.QueryTags.Replace("[", "").Replace("]", "").Replace("\"", ""),
                    IsSuccessful = s.IsSuccessful,
                    MatchedContentTitle = s.ResultContent != null ? s.ResultContent.Title : null
                })
                .ToListAsync();

            // 3. Query per la colonna di DESTRA: Calcolo "al volo" dei Termini Fantasma
            var ghostTermsData = await _context.SearchHistories
                .Where(s => !s.IsSuccessful && !string.IsNullOrEmpty(s.QueryTags))
                .GroupBy(s => s.QueryTags)
                .Select(g => new
                {
                    RawTerm = g.Key,
                    Count = g.Count(),
                    LastTime = g.Max(s => s.Timestamp)
                })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync();

            // Mappatura dei fantasmi pulendo la stringa JSON per la UI
            var topGhostTerms = ghostTermsData.Select(g => new GhostTermDto // Sostituisci con il tuo DTO
            {
                Term = g.RawTerm.Replace("[", "").Replace("]", "").Replace("\"", "").Trim(),
                SearchCount = g.Count,
                LastSearched = g.LastTime
            }).ToList();

            // 4. Impacchettiamo tutto nel ViewModel che la tua parziale si aspetta
            var viewModel = new AkinatorAnalyticsViewModel
            {
                TotalSearches = totalSearches,
                SuccessRate = successRate,
                TotalBounces = totalBounces,
                RecentQueries = recentQueries,
                TopGhostTerms = topGhostTerms
            };

            // 5. Sputiamo fuori la parziale HTML iniettandoci dentro il modello fresco di calcoli
            return Partial("_AkinatorAnalyticsPartial", viewModel);
        }

        // POST: Applica il Soft-Delete senza rimuovere l'articolo dalle query generali
        public async Task<JsonResult> OnPostDeleteArticleAsync([FromBody] DeleteArticleRequest request)
        {
            try
            {
                // Se il model binder ha funzionato, request non è null e request.Id è valorizzato
                if (request == null || request.Id <= 0)
                {
                    return new JsonResult(new { success = false, message = "Errore di mapping: ID non ricevuto correttamente nel Body." });
                }

                // Recuperiamo l'ID reale dall'oggetto della richiesta
                int id = request.Id;

                // Cerchiamo l'articolo nel database
                var article = await _context.Contents.FindAsync(id);

                if (article == null)
                {
                    return new JsonResult(new { success = false, message = $"Articolo con ID #{id} non trovato nel database." });
                }

                // Applichiamo il Soft-Delete
                article.EliminatedAt = DateTime.UtcNow;

                // Logga l'azione nella scatola nera
                var currentUserId = _userManager.GetUserId(User);
                if (!string.IsNullOrEmpty(currentUserId))
                {
                    _context.AdminLogs.Add(new AdminLog
                    {
                        UserId = int.Parse(currentUserId),
                        ActionType = "Eliminazione",
                        Description = $"Spostato nel cestino l'articolo ID #{article.Id}: '{article.Title}'",
                        ExecutedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();

                return new JsonResult(new
                {
                    success = true,
                    message = $"L'articolo '{article.Title}' è stato spostato nel Cestino."
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Errore server: {ex.Message}" });
            }
        }

        // POST: Ripristina un articolo dal Cestino impostandolo come Bozza
        public async Task<JsonResult> OnPostRestoreArticleAsync([FromBody] DeleteArticleRequest request)
        {
            try
            {
                if (request == null || request.Id <= 0)
                {
                    return new JsonResult(new { success = false, message = "Errore di mapping: ID non ricevuto correttamente." });
                }

                int id = request.Id;
                var article = await _context.Contents.FindAsync(id);

                if (article == null)
                {
                    return new JsonResult(new { success = false, message = $"Articolo con ID #{id} non trovato." });
                }

                //  PULIZIA E RESET
                article.EliminatedAt = null;
                article.UpdatedAt = DateTime.UtcNow;

                //  SICUREZZA: Forziamo lo stato a Bozza. 
                // (Sostituisci 'Bozza' con il nome esatto del valore del tuo enum PublishState)
                article.PublishState = PublishState.Bozza;

                // Logga l'azione di ripristino
                var currentUserId = _userManager.GetUserId(User);
                if (!string.IsNullOrEmpty(currentUserId))
                {
                    _context.AdminLogs.Add(new AdminLog
                    {
                        UserId = int.Parse(currentUserId),
                        ActionType = "Ripristino",
                        Description = $"Ripristinato in 'Bozza' l'articolo ID #{article.Id}: '{article.Title}'",
                        ExecutedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();

                return new JsonResult(new
                {
                    success = true,
                    message = $"L'articolo '{article.Title}' è stato ripristinato come Bozza e portato in cima alla lista."
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Errore server durante il ripristino: {ex.Message}" });
            }
        }

        // --- DTO di richiesta per i Form dei Tag ---
        public record CreateTagRequest(string TagName, string CategoryGroup);
        public record CreateSynonymRequest(int TargetTagId, string SynonymWord);
        public record DeleteTagRequest(int Id);

        // --- HANDLERS DENTRO IL PAGEMODEL ---

        // 1. POST: Crea un nuovo Tag
        public async Task<JsonResult> OnPostCreateTagAsync([FromBody] CreateTagRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TagName))
                return new JsonResult(new { success = false, message = "Il nome del tag è obbligatorio." });

            try
            {
                // Controlliamo se esiste già per evitare duplicati
                var exists = await _context.Tags.AnyAsync(t => t.Name.ToLower() == request.TagName.Trim().ToLower());
                if (exists) return new JsonResult(new { success = false, message = "Questo tag esiste già." });

                var newTag = new Tag
                {
                    Name = request.TagName.Trim(),
                    CategoryGroup = request.CategoryGroup?.Trim()
                };

                _context.Tags.Add(newTag);
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true, message = $"Tag #{newTag.Name} creato con successo." });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Errore: {ex.Message}" });
            }
        }

        // 2. POST: Collega un Sinonimo
        public async Task<JsonResult> OnPostCreateSynonymAsync([FromBody] CreateSynonymRequest request)
        {
            if (request == null || request.TargetTagId <= 0 || string.IsNullOrWhiteSpace(request.SynonymWord))
                return new JsonResult(new { success = false, message = "Dati sinonimo non validi." });

            try
            {
                // Qui dipende da come hai strutturato la tabella Sinonimi. 
                // Esempio ipotizzando un'entità 'TagSynonym' legata al Tag principale:
                var tag = await _context.Tags.FindAsync(request.TargetTagId);
                if (tag == null) return new JsonResult(new { success = false, message = "Tag principale non trovato." });

                var newSynonym = new TagSynonym
                {
                    TagId = request.TargetTagId,
                    SynonymName = request.SynonymWord.Trim()
                };

                _context.TagSynonyms.Add(newSynonym);
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true, message = $"Sinonimo '{newSynonym.SynonymName}' collegato a #{tag.Name}." });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Errore: {ex.Message}" });
            }
        }

        // 3. POST: Elimina un Tag
        public async Task<JsonResult> OnPostDeleteTagAsync([FromBody] DeleteTagRequest request)
        {
            if (request == null || request.Id <= 0)
                return new JsonResult(new { success = false, message = "ID Tag non valido." });

            try
            {
                var tag = await _context.Tags.FindAsync(request.Id);
                if (tag == null) return new JsonResult(new { success = false, message = "Tag non trovato." });

                _context.Tags.Remove(tag);
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true, message = "Tag eliminato correttamente dal dizionario." });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Errore durante l'eliminazione: {ex.Message}" });
            }
        }

        //  DTO per la richiesta di cambio ruolo (inviato dal Modal Staff)
        public record UpdateRoleRequest(int UserId, string NewRole);

        //  DTO di supporto interno se vuoi mappare la scrittura del log (opzionale, ma comodo)
        public record CreateAuditLogDto(string ActionType, string OperatorUsername, string Description);

        public async Task<IActionResult> OnPostUpdateRoleAsync([FromBody] UpdateRoleRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.NewRole))
            {
                return new JsonResult(new { success = false, message = "Dati non validi." });
            }

            // Cerchiamo l'utente tramite UserManager
            var fullUser = await _userManager.FindByIdAsync(request.UserId.ToString());
            if (fullUser == null)
            {
                return new JsonResult(new { success = false, message = "Utente non trovato." });
            }

            // Verifichiamo se l'utente ha già lo specifico livello/strato richiesto
            var hasRole = await _userManager.IsInRoleAsync(fullUser, request.NewRole);

            if (hasRole)
            {
                return new JsonResult(new { success = false, message = $"L'utente possiede già lo strato di sicurezza: {request.NewRole}." });
            }

            //  AGGIUNGIAMO IL NUOVO STRATO (Senza toccare gli altri)
            var addResult = await _userManager.AddToRoleAsync(fullUser, request.NewRole);
            if (!addResult.Succeeded)
            {
                return new JsonResult(new { success = false, message = "Errore durante l'assegnazione del nuovo livello di sicurezza." });
            }

            //  SCRITTURA NELL'AUDIT LOG
            // Recupera l'ID dell'utente attualmente loggato che sta usando il pannello Admin
            var operatorIdString = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(operatorIdString) || !int.TryParse(operatorIdString, out int operatorId))
            {
                return new JsonResult(new { success = false, message = "Sessione operatore non valida." });
            }

            var auditLog = new AdminLog
            {
                UserId = operatorId, //  L'ID numerico di chi ha compiuto l'azione (Foreign Key)
                ActionType = "ADD_ROLE_LAYER",
                Description = $"Ha aggiunto lo strato di sicurezza {request.NewRole} ad @{fullUser.UserName}.",
                ExecutedAt = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() //  Opzionale: cattura l'IP visto che hai il campo!
            };

            _context.AdminLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true, message = $"Livello {request.NewRole} assegnato con successo a @{fullUser.UserName}!" });
        }
        //  DTO per la risoluzione rapida di un termine fantasma
        public record ResolveGhostTermRequest(string GhostTerm, string TargetTagName, string? CategoryGroup);
        public async Task<IActionResult> OnPostResolveGhostTermAsync([FromBody] ResolveGhostTermRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.GhostTerm) || string.IsNullOrWhiteSpace(request.TargetTagName))
            {
                return new JsonResult(new { success = false, message = "Dati parziali o non validi." });
            }

            var normalizedTagName = request.TargetTagName.Trim();
            var normalizedGhost = request.GhostTerm.Trim();

            // 1. Verifichiamo se il tag di destinazione esiste già nel sistema
            var existingTag = await _context.Tags
                .FirstOrDefaultAsync(t => t.Name.ToLower() == normalizedTagName.ToLower());

            if (existingTag == null)
            {
                // Se non esiste, creiamo il nuovo Tag ufficiale usando il nome scelto
                existingTag = new Tag
                {
                    Name = normalizedTagName,
                    CategoryGroup = request.CategoryGroup?.Trim() ?? "Generale"
                };
                _context.Tags.Add(existingTag);
                await _context.SaveChangesAsync(); // Genera l'ID del tag
            }

            // 2.  PULIZIA CRONOLOGIA: Sania lo storico in SearchHistory
            // Cerchiamo tutte le ricerche fallite che contenevano la parola fantasma all'interno del campo JSON QueryTags
            var historicalQueries = await _context.SearchHistories
                .Where(s => !s.IsSuccessful && s.QueryTags.ToLower().Contains(normalizedGhost.ToLower()))
                .ToListAsync();

            foreach (var historyRecord in historicalQueries)
            {
                // Convertiamo il fallimento in successo perché ora il sistema ha imparato a riconoscerlo
                historyRecord.IsSuccessful = true;

                // Opzionale: se in futuro vorrai associare al volo anche il contenuto correlato 
                // historyRecord.ResultContentId = ...
            }

            //  3. SCRITTURA NELL'AUDIT LOG (Usando la tua relazione reale)
            var operatorIdString = _userManager.GetUserId(User);
            if (int.TryParse(operatorIdString, out int operatorId))
            {
                _context.AdminLogs.Add(new AdminLog
                {
                    UserId = operatorId,
                    ActionType = "RESOLVE_GHOST_TERM",
                    Description = $"Ha risolto il termine fantasma \"{normalizedGhost}\" convertendolo nel tag ufficiale #{normalizedTagName} e sanando {historicalQueries.Count} ricerche storiche.",
                    ExecutedAt = DateTime.UtcNow,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                });
            }

            // Un unico SaveChangesAsync finale per salvare sia le modifiche alla cronologia che il Log
            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true, message = $"Termine \"{normalizedGhost}\" risolto! {historicalQueries.Count} vecchie ricerche aggregate con successo." });
        }
    }
}

