namespace DataVisualizerApi.DTOs;

public record SeriesWithMeasurementsDto(
    int Id,
    string Name,
    string Unit,
    double? MinValue,
    double? MaxValue,
    string? ColorHex,
    List<MeasurementDto> Measurements
);
