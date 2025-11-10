using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataVisualizerApi.Data;
using DataVisualizerApi.Models;
using DataVisualizerApi.DTOs;

namespace DataVisualizerApi.Controllers;

[ApiController]
[Route("series")]
public class SeriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public SeriesController(AppDbContext db)
    {
        _db = db;
    }

    public record UpdateSeriesColorRequest([MaxLength(7)] string ColorHex);

    private static MeasurementDto ToMeasurementDto(Measurement m) =>
        new(
            Id: m.MeasurementId,
            SeriesId: m.SeriesId,
            MeasuredAt: m.MeasuredAt,
            Value: m.Value
        );

    private static SeriesWithMeasurementsDto ToSeriesWithMeasurementsDto(Series s) =>
        new(
            Id: s.SeriesId,
            Name: s.Name,
            Unit: s.Unit,
            MinValue: s.MinValue,
            MaxValue: s.MaxValue,
            ColorHex: s.ColorHex,
            Measurements: s.Measurements
                .OrderBy(m => m.MeasuredAt)
                .Select(ToMeasurementDto)
                .ToList()
        );

    private static SeriesDto ToDto(Series s) =>
        new(
            Id: s.SeriesId,
            Name: s.Name,
            Unit: s.Unit,
            MinValue: s.MinValue,
            MaxValue: s.MaxValue,
            ColorHex: s.ColorHex
        );

    // GET /series/all-with-measurements-timeframe?from=...&until=...
    [HttpGet("all-with-measurements-timeframe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<SeriesWithMeasurementsDto>>> GetAllWithMeasurementsInTimeframe(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? until = null,
        CancellationToken ct = default)
    {
        // Validate inputs
        if (from.HasValue && until.HasValue && from.Value > until.Value)
        {
            return BadRequest("Parameter 'from' must be earlier than 'until'.");
        }

        var now = DateTime.UtcNow;

        if (until.HasValue && until.Value > now)
        {
            return BadRequest("Parameter 'until' cannot be in the future.");
        }

        if (from.HasValue && from.Value > now)
        {
            return BadRequest("Parameter 'from' cannot be in the future.");
        }

        // Load all series (we still return all series, even if they have no measurements in range)
        var seriesList = await _db.Series
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        // Load measurements within timeframe
        IQueryable<Measurement> measurementsQuery = _db.Measurements.AsNoTracking();

        if (from.HasValue)
        {
            measurementsQuery = measurementsQuery.Where(m => m.MeasuredAt >= from.Value);
        }

        if (until.HasValue)
        {
            measurementsQuery = measurementsQuery.Where(m => m.MeasuredAt <= until.Value);
        }

        var measurements = await measurementsQuery
            .OrderBy(m => m.MeasuredAt)
            .ToListAsync(ct);

        // Group measurements by series
        var measurementsBySeries = measurements
            .GroupBy(m => m.SeriesId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(ToMeasurementDto).ToList()
            );

        // Build result: same DTO as all-with-measurements, but with filtered measurement lists
        var result = seriesList
            .Select(s =>
            {
                var hasMeasurements = measurementsBySeries.TryGetValue(s.SeriesId, out var ms);
                return new SeriesWithMeasurementsDto(
                    Id: s.SeriesId,
                    Name: s.Name,
                    Unit: s.Unit,
                    MinValue: s.MinValue,
                    MaxValue: s.MaxValue,
                    ColorHex: s.ColorHex,
                    Measurements: hasMeasurements ? ms! : new List<MeasurementDto>()
                );
            })
            .ToList();

        return Ok(result);
    }

    // GET /series/{id}
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeriesDto>> GetById(int id, CancellationToken ct = default)
    {
        var series = await _db.Series
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SeriesId == id, ct);

        if (series == null)
            return NotFound();

        return Ok(ToDto(series));
    }

    // POST /series
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeriesDto>> Create(
        [FromBody] SeriesCreateDto req,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (req.MinValue.HasValue && req.MaxValue.HasValue &&
            req.MinValue.Value > req.MaxValue.Value)
        {
            return BadRequest("MinValue cannot be greater than MaxValue.");
        }

        var entity = new Series
        {
            Name = req.Name.Trim(),
            Unit = req.Unit.Trim(),
            MinValue = req.MinValue,
            MaxValue = req.MaxValue,
            ColorHex = string.IsNullOrWhiteSpace(req.ColorHex) ? null : req.ColorHex.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Series.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dto = ToDto(entity);

        return CreatedAtAction(nameof(GetById), new { id = entity.SeriesId }, dto);
    }

    // PATCH /series/{id}/color
    [HttpPatch("{id:int}/color")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeriesDto>> UpdateColor(
        int id,
        [FromBody] UpdateSeriesColorRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.ColorHex))
            return BadRequest("ColorHex is required.");

        if (req.ColorHex.Length > 7)
            return BadRequest("ColorHex must be at most 7 characters.");

        var entity = await _db.Series.FirstOrDefaultAsync(s => s.SeriesId == id, ct);
        if (entity == null)
            return NotFound();

        entity.ColorHex = req.ColorHex.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(entity));
    }

    // PUT /series/{id}
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeriesDto>> Update(
        int id,
        [FromBody] SeriesUpdateDto req,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (req.MinValue.HasValue && req.MaxValue.HasValue &&
            req.MinValue.Value > req.MaxValue.Value)
        {
            return BadRequest("MinValue cannot be greater than MaxValue.");
        }

        var entity = await _db.Series.FirstOrDefaultAsync(s => s.SeriesId == id, ct);
        if (entity == null)
            return NotFound();

        entity.Unit = req.Unit.Trim();
        entity.MinValue = req.MinValue;
        entity.MaxValue = req.MaxValue;
        entity.ColorHex = string.IsNullOrWhiteSpace(req.ColorHex) ? null : req.ColorHex.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(entity));
    }

    // DELETE /series/{id}
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var entity = await _db.Series.FirstOrDefaultAsync(s => s.SeriesId == id, ct);
        if (entity == null)
            return NotFound();

        _db.Series.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
