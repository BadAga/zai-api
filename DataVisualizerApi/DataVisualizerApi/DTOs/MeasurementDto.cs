namespace DataVisualizerApi.DTOs;

public record MeasurementDto(
    long Id,
    int SeriesId,
    DateTime MeasuredAt,
    double Value
);
