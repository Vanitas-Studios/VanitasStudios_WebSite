using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
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

        public AdminInterfaceModel(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            _context = dbContext;
            _userManager = userManager;
        }

        public List<AdminLog> AuditLogs { get; set; } = new();

        public void OnGet()
        {
        }

        #region HANDLERS GET (PARTIAL VIEWS)

        public async Task<PartialViewResult> OnGetGeneralDashboardAsync()
        {
            var viewModel = new DashboardViewModel();

            try
            {
                viewModel.TotalArticlesOnline = await _context.Contents
                    .AsNoTracking()
                    .CountAsync(c => c.PublishState == PublishState.Pubblico);

                viewModel.TotalArticlesDraft = await _context.Contents
                    .AsNoTracking()
                    .CountAsync(c => c.PublishState == PublishState.Bozza);

                viewModel.TotalSearchesExecuted = await _context.SearchHistories.AsNoTracking().CountAsync();

                if (viewModel.TotalSearchesExecuted > 0)
                {
                    int successfulSearches = await _context.SearchHistories.AsNoTracking().CountAsync(sh => sh.IsSuccessful);
                    viewModel.AlgorithmSuccessRate = Math.Round(((double)successfulSearches / viewModel.TotalSearchesExecuted) * 100, 1);
                }

                viewModel.TopArticles = await _context.StatisticalWeights
                    .AsNoTracking()
                    .Where(sw => sw.Content.PublishState == PublishState.Pubblico)
                    .GroupBy(sw => new { sw.ContentId, sw.Content.Title, sw.Content.UpdatedAt })
                    .Select(g => new TopTrendingArticleDto(
                        g.Key.ContentId,
                        g.Key.Title,
                        g.Sum(sw => sw.PopularityWeight),
                        (DateTime)g.Key.UpdatedAt
                    ))
                    .OrderByDescending(a => a.CumulativeWeight)
                    .Take(3)
                    .ToListAsync();

                viewModel.TopTags = await _context.StatisticalWeights
                    .AsNoTracking()
                    .GroupBy(sw => new { sw.TagId, sw.Tag.Name, sw.Tag.CategoryGroup })
                    .Select(g => new TopTagDto(
                        g.Key.TagId,
                        g.Key.Name,
                        g.Key.CategoryGroup,
                        g.Sum(sw => sw.PopularityWeight)
                    ))
                    .OrderByDescending(t => t.TotalGlobalWeight)
                    .Take(5)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ADMIN ERROR - DASHBOARD]: {ex.Message}");
            }

            return Partial("Partials/_GeneralDashboardPartial", viewModel);
        }

        public async Task<PartialViewResult> OnGetArticlesListAsync()
        {
            var viewModel = new ArticlesManagementViewModel();

            try
            {
                viewModel.Articles = await _context.Contents
                    .AsNoTracking()
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new ArticleRowDto(
                        c.Id,
                        c.Title,
                        c.PublishState,
                        c.CreatedAt,
                        (DateTime)c.UpdatedAt,
                        c.Author != null ? c.Author.UserName : "Vanitas Staff",
                        c.ContentTags.Select(ct => ct.Tag != null ? ct.Tag.CategoryGroup : "Senza Tag").FirstOrDefault() ?? "Senza Tag",
                        c.EliminatedAt,
                        c.IsPinned
                    ))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ADMIN ERROR - ARTICLES LIST]: {ex.Message}");
            }

            return Partial("Partials/_ArticlesListPartial", viewModel);
        }

        public async Task<PartialViewResult> OnGetTagsManagementAsync()
        {
            var viewModel = new TagsManagementViewModel();

            try
            {
                viewModel.TagsList = await _context.Tags
                    .AsNoTracking()
                    .OrderBy(t => t.CategoryGroup)
                    .ThenBy(t => t.Name)
                    .Select(t => new TagManagementRowDto(
                        t.Id,
                        t.Name,
                        t.CategoryGroup,
                        t.Synonyms.Select(s => s.SynonymName).ToList()
                    ))
                    .ToListAsync();

                viewModel.AvailableTags = await _context.Tags
                    .AsNoTracking()
                    .OrderBy(t => t.Name)
                    .Select(t => new AvailableTagDto(t.Id, t.Name))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ADMIN ERROR - TAG MANAGEMENT]: {ex.Message}");
            }

            return Partial("Partials/_TagsManagementPartial", viewModel);
        }

        public async Task<PartialViewResult> OnGetStaffManagementAsync()
        {
            var viewModel = new StaffManagementViewModel();

            try
            {
                var usersWithRoles = await _userManager.Users
                    .AsNoTracking()
                    .Select(u => new
                    {
                        u.Id,
                        u.UserName,
                        ArticlesCount = u.AuthoredArticles.Count,
                        RoleIds = _context.UserRoles.Where(ur => ur.UserId == u.Id).Select(ur => ur.RoleId).ToList()
                    })
                    .ToListAsync();

                var rolesMap = await _context.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name);

                foreach (var rawUser in usersWithRoles)
                {
                    string mappedRole = "Utente Base";
                    var userRoleNames = rawUser.RoleIds.Select(id => rolesMap.ContainsKey(id) ? rolesMap[id] : "").ToList();

                    if (userRoleNames.Contains("Admin")) mappedRole = "Admin";
                    else if (userRoleNames.Contains("Editor")) mappedRole = "Editor";

                    viewModel.StaffMembers.Add(new UserRowDto(
                        rawUser.Id,
                        rawUser.UserName ?? "Anonimo",
                        rawUser.ArticlesCount,
                        mappedRole
                    ));
                }

                viewModel.RecentLogs = await _context.AdminLogs
                    .AsNoTracking()
                    .Include(l => l.User)
                    .OrderByDescending(l => l.ExecutedAt)
                    .Take(20)
                    .Select(l => new AdminLogRowDto(
                        l.Id,
                        l.User.UserName ?? "Sistema",
                        l.ActionType,
                        l.Description,
                        l.ExecutedAt
                    ))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ADMIN ERROR - STAFF MANAGEMENT]: {ex.Message}");
            }

            return Partial("Partials/_StaffManagementPartial", viewModel);
        }

        public async Task<IActionResult> OnGetAkinatorAnalyticsAsync()
        {
            var viewModel = new AkinatorAnalyticsViewModel();

            try
            {
                var totalSearches = await _context.SearchHistories.AsNoTracking().CountAsync();
                var totalBounces = await _context.SearchHistories.AsNoTracking().CountAsync(s => !s.IsSuccessful);

                double successRate = totalSearches > 0
                    ? Math.Round((double)(totalSearches - totalBounces) / totalSearches * 100, 1)
                    : 0;

                var recentQueries = await _context.SearchHistories
                    .AsNoTracking()
                    .OrderByDescending(s => s.Timestamp)
                    .Take(15)
                    .Select(s => new SearchHistoryRowDto(
                        s.User != null ? s.User.UserName : "Ospite Anonimo",
                        s.Timestamp,
                        s.QueryTags.Replace("[", "").Replace("]", "").Replace("\"", ""),
                        s.IsSuccessful,
                        s.ResultContent != null ? s.ResultContent.Title : null
                    ))
                    .ToListAsync();

                var ghostTermsData = await _context.SearchHistories
                    .AsNoTracking()
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

                var topGhostTerms = ghostTermsData.Select(g => new GhostTermDto(
                    g.RawTerm.Replace("[", "").Replace("]", "").Replace("\"", "").Trim(),
                    g.Count,
                    g.LastTime
                )).ToList();

                viewModel = new AkinatorAnalyticsViewModel
                {
                    TotalSearches = totalSearches,
                    SuccessRate = successRate,
                    TotalBounces = totalBounces,
                    RecentQueries = recentQueries,
                    TopGhostTerms = topGhostTerms
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ADMIN ERROR - AKINATOR]: {ex.Message}");
            }

            return Partial("Partials/_AkinatorAnalyticsPartial", viewModel);
        }

        public async Task<IActionResult> OnGetAdminLogsAsync()
        {
            AuditLogs = await _context.AdminLogs
                .Include(l => l.User)
                .AsNoTracking()
                .OrderByDescending(l => l.ExecutedAt)
                .Take(100)
                .ToListAsync();

            return Partial("Partials/_AdminLogs", AuditLogs);
        }

        #endregion

        #region HANDLERS POST (ACTIONS)

        public async Task<JsonResult> OnPostDeleteArticleAsync([FromBody] DeleteArticleRequest request)
        {
            if (request == null || request.Id <= 0)
                return new JsonResult(new { success = false, message = "ID della richiesta non valido." });

            try
            {
                var article = await _context.Contents.FindAsync(request.Id);
                if (article == null)
                    return new JsonResult(new { success = false, message = "Articolo non trovato." });

                article.EliminatedAt = DateTime.UtcNow;

                if (TryGetAuthenticatedAdminId(out int currentUserId))
                {
                    _context.AdminLogs.Add(new AdminLog
                    {
                        UserId = currentUserId,
                        ActionType = "DELETE_ARTICLE",
                        Description = $"Spostato nel cestino l'articolo ID #{article.Id}: '{article.Title}'",
                        ExecutedAt = DateTime.UtcNow,
                        IpAddress = GetClientIpAddress()
                    });
                }

                await _context.SaveChangesAsync();
                return new JsonResult(new { success = true, message = $"L'articolo '{article.Title}' è stato spostato nel Cestino." });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Errore server: {ex.Message}" });
            }
        }

        public async Task<JsonResult> OnPostRestoreArticleAsync([FromBody] DeleteArticleRequest request)
        {
            if (request == null || request.Id <= 0)
                return new JsonResult(new { success = false, message = "ID della richiesta non valido." });

            try
            {
                var article = await _context.Contents.FindAsync(request.Id);
                if (article == null)
                    return new JsonResult(new { success = false, message = "Articolo non trovato." });

                article.EliminatedAt = null;
                article.UpdatedAt = DateTime.UtcNow;
                article.PublishState = PublishState.Bozza;

                if (TryGetAuthenticatedAdminId(out int currentUserId))
                {
                    _context.AdminLogs.Add(new AdminLog
                    {
                        UserId = currentUserId,
                        ActionType = "RESTORE_ARTICLE",
                        Description = $"Ripristinato in 'Bozza' l'articolo ID #{article.Id}: '{article.Title}'",
                        ExecutedAt = DateTime.UtcNow,
                        IpAddress = GetClientIpAddress()
                    });
                }

                await _context.SaveChangesAsync();
                return new JsonResult(new { success = true, message = $"L'articolo '{article.Title}' è stato ripristinato come Bozza." });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Errore server: {ex.Message}" });
            }
        }

        public async Task<JsonResult> OnPostTogglePinAsync([FromBody] int id)
        {
            try
            {
                var content = await _context.Contents.FirstOrDefaultAsync(c => c.Id == id);
                if (content == null)
                    return new JsonResult(new { success = false, message = "Articolo non trovato." });

                if (content.PublishState == PublishState.Eliminato || content.EliminatedAt != null)
                    return new JsonResult(new { success = false, message = "Impossibile pinnare un articolo cestinato." });

                content.IsPinned = !content.IsPinned;
                float pinBonus = 150.0f;

                if (content.IsPinned) content.GlobalScore += pinBonus;
                else content.GlobalScore = Math.Max(0.0f, content.GlobalScore - pinBonus);

                content.UpdatedAt = DateTime.UtcNow;

                if (!TryGetAuthenticatedAdminId(out int currentUserId))
                    return new JsonResult(new { success = false, message = "Sessione amministratore scaduta." });

                var log = new AdminLog
                {
                    UserId = currentUserId,
                    ActionType = "TOGGLE_PIN",
                    Description = content.IsPinned
                        ? $"Articolo '{content.Title}' (ID: #{content.Id}) in evidenza. Bonus Akinator (+{pinBonus})."
                        : $"Rimosso l'articolo '{content.Title}' (ID: #{content.Id}) dai Pinned. Bonus Akinator (-{pinBonus}).",
                    ExecutedAt = DateTime.UtcNow,
                    IpAddress = GetClientIpAddress()
                };

                await _context.AdminLogs.AddAsync(log);
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true, isPinned = content.IsPinned, newScore = content.GlobalScore });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CRASH TOGGLE PIN]: {ex.Message}");
                return new JsonResult(new { success = false, message = "Errore hardware o crash interno." });
            }
        }

        public async Task<JsonResult> OnPostCreateTagAsync([FromBody] CreateTagRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TagName))
                return new JsonResult(new { success = false, message = "Il nome del tag è obbligatorio." });

            try
            {
                var exists = await _context.Tags.AnyAsync(t => t.Name.ToLower() == request.TagName.Trim().ToLower());
                if (exists) return new JsonResult(new { success = false, message = "Questo tag esiste già." });

                var newTag = new Tag
                {
                    Name = request.TagName.Trim(),
                    CategoryGroup = request.CategoryGroup?.Trim() ?? "Generale"
                };

                _context.Tags.Add(newTag);
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true, message = $"Tag #{newTag.Name} creato." });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Errore: {ex.Message}" });
            }
        }

        public async Task<JsonResult> OnPostCreateSynonymAsync([FromBody] CreateSynonymRequest request)
        {
            if (request == null || request.TargetTagId <= 0 || string.IsNullOrWhiteSpace(request.SynonymWord))
                return new JsonResult(new { success = false, message = "Dati sinonimo non validi." });

            try
            {
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

                return new JsonResult(new { success = true, message = "Tag eliminato dal dizionario." });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Errore: {ex.Message}" });
            }
        }

        public async Task<JsonResult> OnPostUpdateRoleAsync([FromBody] UpdateRoleRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.NewRole))
                return new JsonResult(new { success = false, message = "Dati non validi." });

            try
            {
                var fullUser = await _userManager.FindByIdAsync(request.UserId.ToString());
                if (fullUser == null) return new JsonResult(new { success = false, message = "Utente non trovato." });

                var hasRole = await _userManager.IsInRoleAsync(fullUser, request.NewRole);
                if (hasRole) return new JsonResult(new { success = false, message = $"L'utente ha già il ruolo {request.NewRole}." });

                var addResult = await _userManager.AddToRoleAsync(fullUser, request.NewRole);
                if (!addResult.Succeeded) return new JsonResult(new { success = false, message = "Errore durante l'assegnazione." });

                if (TryGetAuthenticatedAdminId(out int operatorId))
                {
                    _context.AdminLogs.Add(new AdminLog
                    {
                        UserId = operatorId,
                        ActionType = "ADD_ROLE_LAYER",
                        Description = $"Aggiunto lo strato di sicurezza {request.NewRole} ad @{fullUser.UserName}.",
                        ExecutedAt = DateTime.UtcNow,
                        IpAddress = GetClientIpAddress()
                    });
                    await _context.SaveChangesAsync();
                }

                return new JsonResult(new { success = true, message = $"Livello {request.NewRole} assegnato a @{fullUser.UserName}!" });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Errore: {ex.Message}" });
            }
        }

        public async Task<JsonResult> OnPostResolveGhostTermAsync([FromBody] ResolveGhostTermRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.GhostTerm) || string.IsNullOrWhiteSpace(request.TargetTagName))
                return new JsonResult(new { success = false, message = "Dati parziali o non validi." });

            try
            {
                var normalizedTagName = request.TargetTagName.Trim();
                var normalizedGhost = request.GhostTerm.Trim();

                var existingTag = await _context.Tags.FirstOrDefaultAsync(t => t.Name.ToLower() == normalizedTagName.ToLower());

                if (existingTag == null)
                {
                    existingTag = new Tag
                    {
                        Name = normalizedTagName,
                        CategoryGroup = request.CategoryGroup?.Trim() ?? "Generale"
                    };
                    _context.Tags.Add(existingTag);
                    await _context.SaveChangesAsync();
                }

                var historicalQueries = await _context.SearchHistories
                    .Where(s => !s.IsSuccessful && s.QueryTags.ToLower().Contains(normalizedGhost.ToLower()))
                    .ToListAsync();

                foreach (var historyRecord in historicalQueries)
                {
                    historyRecord.IsSuccessful = true;
                }

                if (TryGetAuthenticatedAdminId(out int operatorId))
                {
                    _context.AdminLogs.Add(new AdminLog
                    {
                        UserId = operatorId,
                        ActionType = "RESOLVE_GHOST_TERM",
                        Description = $"Risolto termine \"{normalizedGhost}\" in tag #{normalizedTagName}. Sanate {historicalQueries.Count} ricerche.",
                        ExecutedAt = DateTime.UtcNow,
                        IpAddress = GetClientIpAddress()
                    });
                }

                await _context.SaveChangesAsync();
                return new JsonResult(new { success = true, message = $"Termine \"{normalizedGhost}\" risolto! {historicalQueries.Count} ricerche sanate." });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Errore: {ex.Message}" });
            }
        }

        #endregion

        #region PRIVATE UTILITIES

        private bool TryGetAuthenticatedAdminId(out int userId)
        {
            userId = 0;
            string? userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return !string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out userId);
        }

        private string GetClientIpAddress()
        {
            var remoteIp = HttpContext.Connection.RemoteIpAddress;
            if (remoteIp == null) return "0.0.0.0";
            return remoteIp.ToString() == "::1" ? "127.0.0.1" : remoteIp.ToString();
        }

        #endregion
    }
    #region UNIFIED VIEWMODELS AND DTOS (RECORDS)

    // --- REQUESTS DTOs ---
    public record DeleteArticleRequest(int Id);
    public record CreateTagRequest(string TagName, string CategoryGroup);
    public record CreateSynonymRequest(int TargetTagId, string SynonymWord);
    public record DeleteTagRequest(int Id);
    public record UpdateRoleRequest(int UserId, string NewRole);
    public record ResolveGhostTermRequest(string GhostTerm, string TargetTagName, string? CategoryGroup);

    // --- DASHBOARD ---
    public class DashboardViewModel
    {
        // Conteggi generali articoli
        public int TotalArticlesOnline { get; set; }
        public int TotalArticlesDraft { get; set; }

        // Metriche di Performance dell'Algoritmo (da SearchHistory)
        public int TotalSearchesExecuted { get; set; }
        public double AlgorithmSuccessRate { get; set; } // Percentuale di successo (es: 84.5%)

        // Top 3 Articoli di Tendenza
        public List<TopTrendingArticleDto> TopArticles { get; set; } = new();

        // Top 5 Argomenti/Tag più caldi
        public List<TopTagDto> TopTags { get; set; } = new();
    }
    public record TopTrendingArticleDto(int Id, string Title, double CumulativeWeight, DateTime UpdatedAt);
    public record TopTagDto(int TagId, string TagName, string Category, double TotalGlobalWeight);

    // --- ARTICLES MANAGEMENT ---
    public class ArticlesManagementViewModel
    {
        public List<ArticleRowDto> Articles { get; set; } = new();
    }
    public record ArticleRowDto(int Id, string Title, PublishState PublishState, DateTime CreatedAt, DateTime UpdatedAt, string AuthorName, string Category, DateTime? EliminatedAt, bool IsPinned);

    // --- TAGS MANAGEMENT ---
    public class TagsManagementViewModel
    {
        public List<TagManagementRowDto> TagsList { get; set; } = new();
        public List<AvailableTagDto> AvailableTags { get; set; } = new();
    }
    public record TagManagementRowDto(int Id, string Name, string CategoryGroup, List<string> Synonyms);
    public record AvailableTagDto(int Id, string Name);

    // --- STAFF MANAGEMENT ---
    public class StaffManagementViewModel
    {
        public List<UserRowDto> StaffMembers { get; set; } = new();
        public List<AdminLogRowDto> RecentLogs { get; set; } = new();
    }
    public record UserRowDto(int Id, string Username, int ArticlesWritten, string Role);
    public record AdminLogRowDto(int Id, string OperatorUsername, string ActionType, string Description, DateTime ExecutedAt);

    // --- AKINATOR ANALYTICS ---
    public class AkinatorAnalyticsViewModel
    {
        public int TotalSearches { get; set; }
        public double SuccessRate { get; set; }
        public int TotalBounces { get; set; }
        public List<SearchHistoryRowDto> RecentQueries { get; set; } = new();
        public List<GhostTermDto> TopGhostTerms { get; set; } = new();
    }
    public record SearchHistoryRowDto(string Username, DateTime Timestamp, string QueryTags, bool IsSuccessful, string? MatchedContentTitle);
    public record GhostTermDto(string Term, int SearchCount, DateTime LastSearched);

    #endregion

}

