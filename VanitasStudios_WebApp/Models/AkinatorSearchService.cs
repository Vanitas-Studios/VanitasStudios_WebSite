using Microsoft.EntityFrameworkCore;
using VanitasStudios_WebApp.Data;
using VanitasStudios_WebApp.Service;

namespace VanitasStudios_WebApp.Models
{
    public class AkinatorSearchService : IAkinatorSearchService
    {
        private readonly ApplicationDbContext _context; // Sostituisci col nome del tuo DbContext

        public AkinatorSearchService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<AutocompleteTagDto>> GetTagSuggestionsAsync(string term, int maxSuggestions = 5)
        {
            if (string.IsNullOrEmpty(term))
                return new List<AutocompleteTagDto>();

            var lowerTerm = term.ToLower();

            return await _context.Tags
                // 1. Cerchiamo direttamente nella tabella dei Tag (e opzionalmente nei sinonimi se inclusi)
                .Where(t => t.Name.ToLower().Contains(lowerTerm) ||
                            t.Synonyms.Any(s => s.SynonymName.ToLower().Contains(lowerTerm)))
                .Select(t => new AutocompleteTagDto
                {
                    TagId = t.Id,
                    TagName = t.Name,
                    Category = t.CategoryGroup,

                    // 2. Se ci sono pesi statistici prendiamo il Max, altrimenti se la tabella è vuota ritorniamo 0
                    MaxArticleWeight = _context.StatisticalWeights
                                        .Where(sw => sw.TagId == t.Id)
                                        .Select(sw => (int?)sw.PopularityWeight)
                                        .Max() ?? 0
                })
                // 3. Ordiniamo per popolarità (i tag più usati scalano la classifica dell'autocomplete)
                .OrderByDescending(t => t.MaxArticleWeight)
                .Take(maxSuggestions)
                .ToListAsync();
        }

        public async Task<AkinatorResultDto> ExecuteSearchAsync(string userText, List<int> selectedTagIds)
        {
            var result = new AkinatorResultDto();
            selectedTagIds ??= new List<int>();

            // ------------------------------------------------------------------
            // STEP 1: Recupero la base dei candidati (Tutti gli articoli online)
            // ------------------------------------------------------------------
            var articles = await _context.Contents
                .Where(c => c.PublishState == PublishState.Pubblico) // Sostituisci con la tua Enum di Stato
                .ToListAsync();

            if (!articles.Any())
            {
                result.IsFinalResult = true;
                return result;
            }

            // Mappa di supporto locale per tracciare i punteggi di sessione di ogni articolo
            var scores = articles.ToDictionary(a => a.Id, a => 0.0);

            // ------------------------------------------------------------------
            // STEP 2: Analisi del testo libero (Input dell'utente)
            // ------------------------------------------------------------------
            if (!string.IsNullOrWhiteSpace(userText))
            {
                // Pulizia testo e split in parole chiavi
                var keywords = userText.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var article in articles)
                {
                    string titleLower = article.Title.ToLower();

                    // Logica A: Bonus pesante se la stringa intera è contenuta nel titolo
                    if (titleLower.Contains(userText.ToLower()))
                    {
                        scores[article.Id] += 150.0;
                    }
                    else
                    {
                        // Logica B: Bonus parziale per singole parole trovate nel titolo
                        foreach (var word in keywords)
                        {
                            if (titleLower.Contains(word))
                                scores[article.Id] += 40.0;
                        }
                    }
                }

                // Logica C: Espansione tramite Sinonimi. 
                // Se una parola dell'utente corrisponde a un sinonimo, ne estraiamo il TagId reale
                var matchedTagIdsFromSynonyms = await _context.TagSynonyms
                    .Where(s => keywords.Contains(s.SynonymName.ToLower()))
                    .Select(s => s.TagId)
                    .ToListAsync();

                // Uniamo i tag trovati via testo a quelli già esplicitamente selezionati
                selectedTagIds = selectedTagIds.Union(matchedTagIdsFromSynonyms).Distinct().ToList();
            }

            // ------------------------------------------------------------------
            // STEP 3: Calcolo dei StatisticalWeights con DECADIMENTO TEMPORALE
            // ------------------------------------------------------------------
            if (selectedTagIds.Any())
            {
                var weights = await _context.StatisticalWeights
                    .Where(sw => selectedTagIds.Contains(sw.TagId))
                    .ToListAsync();

                foreach (var weight in weights)
                {
                    if (scores.ContainsKey(weight.ContentId))
                    {
                        var article = articles.First(a => a.Id == weight.ContentId);

                        // Calcoliamo quanti giorni fa è stato pubblicato l'articolo
                        var daysSinceRelease = (DateTime.UtcNow - (article.UpdatedAt ?? DateTime.UtcNow)).TotalDays;
                        if (daysSinceRelease < 0) daysSinceRelease = 0; // Protezione da anomalie di date future

                        // --- FORMULA DI DECADIMENTO ---
                        // Più passano i giorni, più il divisore aumenta, riducendo l'impatto del peso storico.
                        // Es: dopo 30 giorni il peso si dimezza. Dopo 90 giorni si riduce a 1/4.
                        double timeHalvingInterval = 30.0; // Ogni quanti giorni vogliamo dimezzare l'impatto
                        double decayFactor = 1.0 / (1.0 + (daysSinceRelease / timeHalvingInterval));

                        // Applichiamo il fattore di decadimento al PopularityWeight reale del DB
                        double decayedWeight = weight.PopularityWeight * decayFactor;

                        scores[weight.ContentId] += decayedWeight;
                    }
                }
            }

            // ------------------------------------------------------------------
            // STEP 4: Tie-Breaker (Risoluzione parità con la freschezza dei contenuti)
            // ------------------------------------------------------------------
            foreach (var articleId in scores.Keys.ToList())
            {
                var article = articles.First(a => a.Id == articleId);
                var daysSinceRelease = (DateTime.UtcNow - (article.UpdatedAt ?? DateTime.UtcNow)).TotalDays;

                double freshnessBonus = 5.0 / (1.0 + daysSinceRelease); // Bonus minimo (max +5 punti)
                scores[articleId] += freshnessBonus;
            }


            // ------------------------------------------------------------------
            // STEP 5: Ordinamento Finale e Valutazione del Bivio Akinator
            // ------------------------------------------------------------------
            var rankedCandidates = scores
                .Where(kv => kv.Value > 0)
                .OrderByDescending(kv => kv.Value)
                .Select(kv => articles.First(a => a.Id == kv.Key))
                .ToList();

            if (!rankedCandidates.Any())
            {
                result.IsFinalResult = true;
                return result;
            }

            var topCandidate = rankedCandidates.First();
            double topScore = scores[topCandidate.Id];
            double secondScore = rankedCandidates.Count > 1 ? scores[rankedCandidates[1].Id] : 0.0;
            double scoreGap = topScore - secondScore;

            // Se l'algoritmo ha le idee chiare o abbiamo fatto troppe domande
            if (rankedCandidates.Count == 1 || scoreGap > 40.0 || selectedTagIds.Count >= 5)
            {
                result.IsFinalResult = true;

                // MAPPATURA DI SICUREZZA: Trasformiamo i Content nel DTO controllato
                result.Articles = rankedCandidates.Select(c => new SearchArticleResultDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    Slug = c.Slug,
                    CoverImageUrl = !string.IsNullOrEmpty(c.CoverImageUrl) ? c.CoverImageUrl : "/media/placeholder-default.png",
                    FormattedDate = c.CreatedAt.ToString("dd/MM/yyyy")
                }).ToList();

                return result;
            }


            // ------------------------------------------------------------------
            // STEP 6: Modalità Akinator - Estrazione del Tag discriminante
            // ------------------------------------------------------------------
            var topCandidateIds = rankedCandidates.Take(3).Select(c => c.Id).ToList();

            var nextBestTag = await _context.StatisticalWeights
                .Where(sw => topCandidateIds.Contains(sw.ContentId) && !selectedTagIds.Contains(sw.TagId))
                .GroupBy(sw => new { sw.TagId, sw.Tag.Name })
                .Select(g => new
                {
                    TagId = g.Key.TagId,
                    TagName = g.Key.Name,
                    TotalWeightInFinalists = g.Sum(sw => sw.PopularityWeight)
                })
                .OrderByDescending(t => t.TotalWeightInFinalists)
                .FirstOrDefaultAsync();

            if (nextBestTag != null)
            {
                result.IsFinalResult = false;
                result.NextTagIdSuggested = nextBestTag.TagId;
                result.NextQuestionText = nextBestTag.TagName;
            }
            else
            {
                result.IsFinalResult = true;

                // MAPPATURA DI SICUREZZA ANCHE IN USCITA FORZATA
                result.Articles = rankedCandidates.Take(3).Select(c => new SearchArticleResultDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    Slug = c.Slug,
                    CoverImageUrl = !string.IsNullOrEmpty(c.CoverImageUrl) ? c.CoverImageUrl : "/media/placeholder-default.png",
                    FormattedDate = c.CreatedAt.ToString("dd/MM/yyyy")
                }).ToList();
            }

            return result;
        }
    }
}
