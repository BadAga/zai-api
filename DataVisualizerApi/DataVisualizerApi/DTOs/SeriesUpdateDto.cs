using System.ComponentModel.DataAnnotations;

namespace DataVisualizerApi.DTOs
{
    public class SeriesUpdateDto
    {
        [Required]
        [MaxLength(20)]
        public string Unit { get; set; } = default!;

        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }

        [MaxLength(7)]
        public string? ColorHex { get; set; }
    }
}
