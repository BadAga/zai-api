namespace DataVisualizerApi.DTOs
{
    public record SeriesDto(
        int Id,
        string Name,
        string Unit,
        double? MinValue,
        double? MaxValue,
        string? ColorHex
    );
}
