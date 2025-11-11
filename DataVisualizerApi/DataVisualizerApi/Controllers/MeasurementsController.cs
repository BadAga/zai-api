using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataVisualizerApi.Data;
using DataVisualizerApi.Models;
using DataVisualizerApi.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace DataVisualizerApi.Controllers;

[ApiController]
[Route("measurements")]
public class MeasurementsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MeasurementsController(AppDbContext db)
    {
        _db = db;
    }

    public class MeasurementCreateUpdateRequest
    {
        [Required]
        public int SeriesId { get; set; }

        [Required]
        public DateTime MeasuredAt { get; set; }

        [Required]
        public double Value { get; set; }
    }

    private static MeasurementDto ToDto(Measurement m) =>
        new(
            Id: m.MeasurementId,
            SeriesId: m.SeriesId,
            MeasuredAt: m.MeasuredAt,
            Value: m.Value
        );

    // GET /measurements?seriesId=1&from=2025-01-01&to=2025-12-31
    [HttpGet()]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<MeasurementDto>>> GetBySeries(
        [FromQuery] int seriesId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        if (seriesId <= 0)
            return BadRequest("seriesId is required and must be > 0.");

        var query = _db.Measurements
            .AsNoTracking()
            .Where(m => m.SeriesId == seriesId);

        if (from.HasValue)
            query = query.Where(m => m.MeasuredAt >= from.Value);

        if (to.HasValue)
            query = query.Where(m => m.MeasuredAt <= to.Value);

        var list = await query
            .OrderBy(m => m.MeasuredAt)
            .Select(m => ToDto(m))
            .ToListAsync(ct);

        return Ok(list);
    }

    // GET /measurements/{id}
    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MeasurementDto>> GetById(long id, CancellationToken ct = default)
    {
        var m = await _db.Measurements
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MeasurementId == id, ct);

        if (m == null)
            return NotFound();

        return Ok(ToDto(m));
    }

    // POST /measurements
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MeasurementDto>> Create(
        [FromBody] MeasurementCreateUpdateRequest req,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // 1) Sprawdź, czy seria istnieje
        var series = await _db.Series
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SeriesId == req.SeriesId, ct);

        if (series == null)
            return BadRequest($"Series with id {req.SeriesId} does not exist.");

        // 2) Walidacja min/max
        if (series.MinValue.HasValue && req.Value < series.MinValue.Value)
            return BadRequest($"Value {req.Value} is below MinValue {series.MinValue.Value} for this series.");

        if (series.MaxValue.HasValue && req.Value > series.MaxValue.Value)
            return BadRequest($"Value {req.Value} is above MaxValue {series.MaxValue.Value} for this series.");

        var entity = new Measurement
        {
            SeriesId = req.SeriesId,
            MeasuredAt = req.MeasuredAt,
            Value = req.Value,
            CreatedAt = DateTime.UtcNow
        };

        _db.Measurements.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dto = ToDto(entity);

        return CreatedAtAction(nameof(GetById), new { id = entity.MeasurementId }, dto);
    }

    // PUT /measurements/{id}
    [HttpPut("{id:long}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MeasurementDto>> Update(
        long id,
        [FromBody] MeasurementCreateUpdateRequest req,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var entity = await _db.Measurements.FirstOrDefaultAsync(m => m.MeasurementId == id, ct);
        if (entity == null)
            return NotFound();

        // Pobierz serię do walidacji min/max (może się też zmienić SeriesId)
        var series = await _db.Series
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SeriesId == req.SeriesId, ct);

        if (series == null)
            return BadRequest($"Series with id {req.SeriesId} does not exist.");

        if (series.MinValue.HasValue && req.Value < series.MinValue.Value)
            return BadRequest($"Value {req.Value} is below MinValue {series.MinValue.Value} for this series.");

        if (series.MaxValue.HasValue && req.Value > series.MaxValue.Value)
            return BadRequest($"Value {req.Value} is above MaxValue {series.MaxValue.Value} for this series.");

        entity.SeriesId = req.SeriesId;
        entity.MeasuredAt = req.MeasuredAt;
        entity.Value = req.Value;
        // CreatedAt zostawiamy bez zmian

        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(entity));
    }

    // DELETE /measurements/{id}
    [HttpDelete("{id:long}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct = default)
    {
        var entity = await _db.Measurements.FirstOrDefaultAsync(m => m.MeasurementId == id, ct);
        if (entity == null)
            return NotFound();

        _db.Measurements.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
