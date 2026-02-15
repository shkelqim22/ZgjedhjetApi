using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZgjedhjetApi.Data;
using ZgjedhjetApi.Enums;
using ZgjedhjetApi.Models.DTOs;
using ZgjedhjetApi.Models.Entities;

namespace ZgjedhjetApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ZgjedhjetController : ControllerBase
    {
        private readonly ILogger<ZgjedhjetController> _logger;
        private readonly LifeDbContext _db;

        public ZgjedhjetController(ILogger<ZgjedhjetController> logger, LifeDbContext db)
        {
            _logger = logger;
            _db = db;
        }


        [HttpPost("import")]
        public async Task<ActionResult<CsvImportResponse>> MigrateData(IFormFile file)
        {
            var response = new CsvImportResponse();

            await using var transaction = await _db.Database.BeginTransactionAsync();


            if (file == null || file.Length == 0)
            {
                response.Success = false;
                response.Message = "No file has been uploaded.";
                return BadRequest(response);
            }

            var imported = new List<Zgjedhjet>();
            var errors = new List<string>();
            var lineNumber = 1;

            try
            {
                using var stream = file.OpenReadStream();
                using var reader = new StreamReader(stream);

                var headerLine = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(headerLine))
                {
                    response.Success = false;
                    response.Message = "The CSV header is empty.";
                    return BadRequest(response);
                }

                headerLine = headerLine.Trim();
                if (headerLine.Length > 0 && headerLine[0] == '\uFEFF')
                {
                    headerLine = headerLine.Substring(1);
                }

                var headers = headerLine
                    .Split(',', StringSplitOptions.None)
                    .Select(h => h.Trim().Trim('"'))
                    .ToArray();


                int idxKategoria = Array.FindIndex(headers, h => string.Equals(h, "Kategoria", StringComparison.OrdinalIgnoreCase));
                int idxKomuna = Array.FindIndex(headers, h => string.Equals(h, "Komuna", StringComparison.OrdinalIgnoreCase));
                int idxQendra = Array.FindIndex(headers, h => h != null && h.StartsWith("Qendra", StringComparison.OrdinalIgnoreCase));
                int idxVend = Array.FindIndex(headers, h => h != null && (h.StartsWith("Vend", StringComparison.OrdinalIgnoreCase) || string.Equals(h, "Vendvotimi", StringComparison.OrdinalIgnoreCase)));

                if (idxKategoria < 0 || idxKomuna < 0 || idxQendra < 0 || idxVend < 0)
                {
                    response.Success = false;
                    response.Message = $"Required columns not found. Found headers: [{string.Join(", ", headers)}]";
                    return BadRequest(response);
                }


                var partyColumns = new List<(int Index, string Header)>();
                for (int i = 0; i < headers.Length; i++)
                {
                    var h = headers[i];
                    if (!string.IsNullOrWhiteSpace(h) && h.StartsWith("Partia", StringComparison.OrdinalIgnoreCase))
                        partyColumns.Add((i, h));
                }

                if (partyColumns.Count == 0)
                {
                    response.Success = false;
                    response.Message = "No party columns detected (headers starting with 'Partia').";
                    return BadRequest(response);
                }

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(',', StringSplitOptions.None).Select(p => p.Trim().Trim('"')).ToArray();


                    if (parts.Length < headers.Length)
                    {
                        errors.Add($"Line {lineNumber}: column count {parts.Length} less than header count {headers.Length}.");
                        continue;
                    }

                    var rawKategoria = parts[idxKategoria];
                    var rawKomuna = parts[idxKomuna];
                    var rawQendra = parts[idxQendra];
                    var rawVend = parts[idxVend];

                    if (!Enum.TryParse<Kategoria>(rawKategoria, true, out var kategoria))
                    {
                        errors.Add($"Line {lineNumber}: invalid Kategoria '{rawKategoria}'.");
                        continue;
                    }

                    if (!Enum.TryParse<Komuna>(rawKomuna, true, out var komuna))
                    {
                        errors.Add($"Line {lineNumber}: invalid Komuna '{rawKomuna}'.");
                        continue;
                    }


                    foreach (var (idx, partyHeader) in partyColumns)
                    {
                        var rawVotes = parts[idx];
                        if (string.IsNullOrWhiteSpace(rawVotes))
                            continue;

                        if (!int.TryParse(rawVotes, out var votes))
                            continue;

                        if (votes == 0)
                            continue; 


                        var enumName = partyHeader.Replace(" ", "", StringComparison.OrdinalIgnoreCase);
                        if (!Enum.TryParse<Partia>(enumName, true, out var partia))
                        {
                            var alt = enumName.Replace("Partia", "Partia", StringComparison.OrdinalIgnoreCase).Replace(" ", "", StringComparison.OrdinalIgnoreCase);
                         
                            if (!Enum.TryParse<Partia>(alt, true, out partia))
                            {
                                errors.Add($"Line {lineNumber}: unknown party header '{partyHeader}'.");
                                continue;
                            }
                        }

                        var entity = new Zgjedhjet
                        {
                            Kategoria = kategoria,
                            Komuna = komuna,
                            Qendra_e_Votimit = rawQendra,
                            VendVotimi = rawVend,
                            Partia = partia,
                            Vota = votes
                        };

                        imported.Add(entity);
                    }
                }

                if (errors.Any())
                {
                    response.Success = false;
                    response.Message = "Some rows failed to parse.";
                    response.RecordsImported = 0;
                    response.Errors = errors;
                    return BadRequest(response);
                }

                if (imported.Count == 0)
                {
                    response.Success = false;
                    response.Message = "No records to import (no votes > 0 found).";
                    return BadRequest(response);
                }

                await _db.Zgjedhjet.ExecuteDeleteAsync();
                await _db.Zgjedhjet.AddRangeAsync(imported);
                await _db.SaveChangesAsync();

                await transaction.CommitAsync();


                response.Success = true;
                response.RecordsImported = imported.Count;
                response.Message = $"Successfully imported {imported.Count} records.";
                response.Errors = new List<string>();
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing CSV data at line {Line}", lineNumber);
                response.Success = false;
                response.Message = "An error occurred while importing CSV.";
                response.Errors.Add(ex.Message);
                return StatusCode(500, response);
            }
        }

  
        [HttpGet]
        public async Task<ActionResult<ZgjedhjetAggregatedResponse>> GetZgjedhjet(
            [FromQuery] Kategoria? kategoria = null,
            [FromQuery] Komuna? komuna = null,
            [FromQuery] string? qendra_e_votimit = null,
            [FromQuery] string? vendvotimi = null,
            [FromQuery] Partia? partia = null)
        {
            var query = _db.Zgjedhjet.AsQueryable();
            var response = new ZgjedhjetAggregatedResponse();

            try
            {
                if (kategoria.HasValue && kategoria.Value != Kategoria.TeGjitha)
                {
                    query = query.Where(x => x.Kategoria == kategoria.Value);
                }

                if (komuna.HasValue && komuna.Value != Komuna.TeGjitha)
                {
                    query = query.Where(x => x.Komuna == komuna.Value);
                }

                if (!string.IsNullOrWhiteSpace(qendra_e_votimit))
                {
                    var exists = await _db.Zgjedhjet.AnyAsync(x => x.Qendra_e_Votimit == qendra_e_votimit);
                    if (!exists)
                    {
                        return NotFound(new { message = $"Qendra e Votimit '{qendra_e_votimit}' not found." });
                    }
                    query = query.Where(x => x.Qendra_e_Votimit == qendra_e_votimit);
                }

                if (!string.IsNullOrWhiteSpace(vendvotimi))
                {
                    var exists = await _db.Zgjedhjet.AnyAsync(x => x.VendVotimi == vendvotimi);
                    if (!exists)
                    {
                        return NotFound(new { message = $"Vend Votimi '{vendvotimi}' not found." });
                    }
                    query = query.Where(x => x.VendVotimi == vendvotimi);
                }

                if (partia.HasValue && partia.Value != Partia.TeGjitha)
                {
                    query = query.Where(x => x.Partia == partia.Value);
                }

                var aggregated = await query
                    .GroupBy(x => x.Partia)
                    .Select(g => new PartiaVotesResponse
                    {
                        Partia = g.Key.ToString(),
                        TotalVota = g.Sum(x => x.Vota)
                    })
                    .ToListAsync(); 

                response.Results = aggregated;
                return Ok(response);
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Error retrieving Zgjedhjet");

                response.Results = new List<PartiaVotesResponse>();
                return StatusCode(500, response);
            }
        }
    }
}
