using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nest;
using StackExchange.Redis;
using ZgjedhjetApi.Data;
using ZgjedhjetApi.Enums;
using ZgjedhjetApi.Models.Documents;
using ZgjedhjetApi.Models.DTOs;

namespace ZgjedhjetApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ZgjedhjetElasticSearchController : ControllerBase
    {
        private const string IndexName = "zgjedhjet";

        private readonly ILogger<ZgjedhjetElasticSearchController> _logger;
        private readonly LifeDbContext _db;
        private readonly IElasticClient _es;
        private readonly IDatabase _redis;

        public ZgjedhjetElasticSearchController(
            ILogger<ZgjedhjetElasticSearchController> logger,
            LifeDbContext db,
            IElasticClient es,
            IConnectionMultiplexer redisConnection)
        {
            _logger = logger;
            _db = db;
            _es = es;
            _redis = redisConnection.GetDatabase();
        }

  

        [HttpPost("migrate")]
        public async Task<ActionResult> MigrateToElasticsearch()
        {
            try
            {
                var exists = await _es.Indices.ExistsAsync(IndexName);
                if (exists.Exists)
                    await _es.Indices.DeleteAsync(IndexName);

                await EnsureIndexAsync();

                var records = await _db.Zgjedhjet.AsNoTracking().ToListAsync();
                if (records.Count == 0)
                    return BadRequest(new { message = "No records found in SQL database to migrate." });

                var documents = records.Select(r => new ZgjedhjetDocument
                {
                    Id = r.Id,
                    Kategoria = r.Kategoria.ToString(),
                    Komuna = r.Komuna.ToString(),
                    Qendra_e_Votimit = r.Qendra_e_Votimit,
                    VendVotimi = r.VendVotimi,
                    Partia = r.Partia.ToString(),
                    Vota = r.Vota
                }).ToList();


                await _es.DeleteByQueryAsync<ZgjedhjetDocument>(d => d
                    .Index(IndexName)
                    .Query(q => q.MatchAll()));

                var bulkResponse = await _es.BulkAsync(b => b
                    .Index(IndexName)
                    .IndexMany(documents));

                if (bulkResponse.Errors)
                {
                    var firstError = bulkResponse.ItemsWithErrors.FirstOrDefault()?.Error?.Reason;
                    return StatusCode(500, new { message = "Bulk indexing had errors.", detail = firstError });
                }

                return Ok(new
                {
                    message = $"Successfully migrated {documents.Count} records to Elasticsearch.",
                    recordsMigrated = documents.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error migrating data to Elasticsearch");
                return StatusCode(500, new { message = "An error occurred during migration.", detail = ex.Message });
            }
        }



        [HttpGet]
        public async Task<ActionResult<ZgjedhjetAggregatedResponse>> GetZgjedhjet(
            [FromForm] Kategoria? kategoria = null,
            [FromForm] Komuna? komuna = null,
            [FromForm] string? qendra_e_votimit = null,
            [FromForm] string? vendvotimi = null,
            [FromForm] Partia? partia = null)
        {
            var response = new ZgjedhjetAggregatedResponse();

            try
            {
                var mustClauses = new List<Func<QueryContainerDescriptor<ZgjedhjetDocument>, QueryContainer>>();

                if (kategoria.HasValue && kategoria.Value != Kategoria.TeGjitha)
                    mustClauses.Add(q => q.Term(t => t.Field(f => f.Kategoria).Value(kategoria.Value.ToString())));

                if (komuna.HasValue && komuna.Value != Komuna.TeGjitha)
                    mustClauses.Add(q => q.Term(t => t.Field("komuna.keyword").Value(komuna.Value.ToString())));

                if (!string.IsNullOrWhiteSpace(qendra_e_votimit))
                {
                    var existsResp = await _es.SearchAsync<ZgjedhjetDocument>(s => s
                        .Index(IndexName)
                        .Size(0)
                        .Query(q => q.Term(t => t.Field("qendra_e_Votimit.keyword").Value(qendra_e_votimit))));

                    if (existsResp.Total == 0)
                        return NotFound(new { message = $"Qendra e Votimit '{qendra_e_votimit}' not found." });

                    mustClauses.Add(q => q.Term(t => t.Field("qendra_e_Votimit.keyword").Value(qendra_e_votimit)));
                }


                if (!string.IsNullOrWhiteSpace(vendvotimi))
                {
                    var existsResp = await _es.SearchAsync<ZgjedhjetDocument>(s => s
                        .Index(IndexName)
                        .Size(0)
                        .Query(q => q.Term(t => t.Field("vendVotimi.keyword").Value(vendvotimi))));

                    if (existsResp.Total == 0)
                        return NotFound(new { message = $"Vend Votimi '{vendvotimi}' not found." });

                    mustClauses.Add(q => q.Term(t => t.Field("vendVotimi.keyword").Value(vendvotimi)));
                }

                if (partia.HasValue && partia.Value != Partia.TeGjitha)
                    mustClauses.Add(q => q.Term(t => t.Field(f => f.Partia).Value(partia.Value.ToString())));

                var searchResponse = await _es.SearchAsync<ZgjedhjetDocument>(s => s
                    .Index(IndexName)
                    .Size(0)
                    .Query(q => mustClauses.Count > 0
                        ? q.Bool(b => b.Must(mustClauses.ToArray()))
                        : q.MatchAll())
                    .Aggregations(a => a
                        .Terms("by_partia", t => t
                            .Field(f => f.Partia)
                            .Size(1000)
                            .Aggregations(sub => sub
                                .Sum("total_vota", sum => sum
                                    .Field(f => f.Vota))))));

                if (!searchResponse.IsValid)
                {
                    _logger.LogError("Elasticsearch query failed: {Debug}", searchResponse.DebugInformation);
                    return StatusCode(500, response);
                }

                var results = new List<PartiaVotesResponse>();

                var termsAgg = searchResponse.Aggregations.Terms("by_partia");
                foreach (var bucket in termsAgg.Buckets)
                {
                    var sumAgg = bucket.Sum("total_vota");
                    results.Add(new PartiaVotesResponse
                    {
                        Partia = bucket.Key,
                        TotalVota = (int)(sumAgg.Value ?? 0)
                    });
                }

                response.Results = results;
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Zgjedhjet from Elasticsearch");
                response.Results = new List<PartiaVotesResponse>();
                return StatusCode(500, response);
            }
        }

     

        [HttpGet("suggest")]
        public async Task<ActionResult<List<string>>> SuggestKomuna([FromQuery] string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Ok(new List<string>());

            try
            {
                var searchResponse = await _es.SearchAsync<ZgjedhjetDocument>(s => s
                    .Index(IndexName)
                    .Size(0)
                    .Query(q => q
                        .Bool(b => b
                            .Should(
                                sh => sh.MatchPhrasePrefix(m => m
                                    .Field("komuna")
                                    .Query(query)),
                                sh => sh.Match(m => m
                                    .Field("komuna")
                                    .Query(query)
                                    .Fuzziness(Fuzziness.Auto))
                            )
                            .MinimumShouldMatch(1)))
                    .Aggregations(a => a
                        .Terms("unique_komunat", t => t
                            .Field("komuna.keyword")
                            .Size(100))));

                if (!searchResponse.IsValid)
                {
                    _logger.LogError("ES suggest query failed: {Debug}", searchResponse.DebugInformation);
                    return StatusCode(500, new List<string>());
                }

                var suggestions = searchResponse.Aggregations
                    .Terms("unique_komunat")
                    .Buckets
                    .Select(b => b.Key)
                    .ToList();

                if (suggestions.Count > 0)
                    _ = RecordSuggestionStatsAsync(suggestions);

                return Ok(suggestions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error suggesting Komuna");
                return StatusCode(500, new List<string>());
            }
        }

        

        [HttpGet("suggest/stats")]
        public async Task<ActionResult<List<KomunaStatsResponse>>> GetSuggestionStats(
            [FromQuery] int top = 10)
        {
            try
            {
                const string leaderboardKey = "komuna:suggestions:leaderboard";

                var entries = await _redis.SortedSetRangeByRankWithScoresAsync(
                    leaderboardKey,
                    start: 0,
                    stop: top - 1,
                    order: Order.Descending);

                var result = entries.Select(e => new KomunaStatsResponse
                {
                    Komuna = e.Element!,
                    NrISugjerimeve = (int)e.Score
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading suggestion stats from Redis");
                return StatusCode(500, new List<KomunaStatsResponse>());
            }
        }

        

        private async Task RecordSuggestionStatsAsync(List<string> suggestedKomunat)
        {
            try
            {
                const string leaderboardKey = "komuna:suggestions:leaderboard";
                var batch = _redis.CreateBatch();
                var tasks = suggestedKomunat
                    .Select(k => batch.SortedSetIncrementAsync(leaderboardKey, k, 1))
                    .ToList();
                batch.Execute();
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record suggestion stats in Redis");
            }
        }

        private async Task EnsureIndexAsync()
        {
            await _es.Indices.CreateAsync(IndexName, c => c
                .Settings(s => s
                    .Analysis(a => a
                        .CharFilters(cf => cf
                            .Mapping("underscore_to_space", m => m
                                .Mappings("_ => \\u0020")))
                        .Analyzers(an => an
                            .Custom("komunat_analyzer", ca => ca
                                .CharFilters("underscore_to_space")
                                .Tokenizer("standard")
                                .Filters("lowercase", "asciifolding")))))
                .Map<ZgjedhjetDocument>(m => m
                    .Properties(p => p
                        .Number(n => n.Name(d => d.Id).Type(NumberType.Integer))
                        .Keyword(k => k.Name(d => d.Kategoria))
                        .Text(t => t
                            .Name(d => d.Komuna)
                            .Analyzer("komunat_analyzer")
                            .Fields(f => f.Keyword(k => k.Name("keyword"))))
                        .Text(t => t
                            .Name(d => d.Qendra_e_Votimit)
                            .Fields(f => f.Keyword(k => k.Name("keyword"))))
                        .Text(t => t
                            .Name(d => d.VendVotimi)
                            .Fields(f => f.Keyword(k => k.Name("keyword"))))
                        .Keyword(k => k.Name(d => d.Partia))
                        .Number(n => n.Name(d => d.Vota).Type(NumberType.Integer)))));
        }
    }
}

