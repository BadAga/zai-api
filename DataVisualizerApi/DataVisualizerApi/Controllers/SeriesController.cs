using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataVisualizerApi.Data;
using DataVisualizerApi.Models;

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

    // DTOs
    public record SeriesDto(
        int Id,
        string Name,
        string Unit,
        double? MinValue,
        double? MaxValue,
        string? ColorHex
    );

    public class SeriesCreateUpdateRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = default!;

        [Required]
        [MaxLength(20)]
        public string Unit { get; set; } = default!;

        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }

        [MaxLength(7)]
        public string? ColorHex { get; set; }
    }

    public record UpdateSeriesColorRequest([MaxLength(7)] string ColorHex);

    public record SeriesWithMeasurementsDto(
    int Id,
    string Name,
    string Unit,
    double? MinValue,
    double? MaxValue,
    string? ColorHex,
    List<MeasurementDto> Measurements
);

    public record MeasurementDto(
        long Id,
        int SeriesId,
        DateTime MeasuredAt,
        double Value
    );

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

    // GET /series
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetAll(CancellationToken ct = default)
    {
        var list = await _db.Series
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => ToDto(s))
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpGet("all-with-measurements")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetAllWithMeasurements(CancellationToken ct = default)
    {
        var list = await _db.Series
        .AsNoTracking()
        .Include(s => s.Measurements)
        .OrderBy(s => s.Name)
        .Select(s => ToSeriesWithMeasurementsDto(s))
        .ToListAsync(ct);

        return Ok(list);
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
        [FromBody] SeriesCreateUpdateRequest req,
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
        [FromBody] SeriesCreateUpdateRequest req,
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

        entity.Name = req.Name.Trim();
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
