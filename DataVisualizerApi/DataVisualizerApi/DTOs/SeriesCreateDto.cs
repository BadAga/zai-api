using System.ComponentModel.DataAnnotations;

namespace DataVisualizerApi.DTOs
{
    public class SeriesCreateDto
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
}
