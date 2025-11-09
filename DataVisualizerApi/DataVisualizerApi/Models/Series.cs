using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataVisualizerApi.Models;

public partial class Series
{
    [Key]
    public int SeriesId { get; set; }

    [StringLength(100)]
    public string Name { get; set; } = null!;

    [StringLength(20)]
    public string Unit { get; set; } = null!;

    public double? MinValue { get; set; }

    public double? MaxValue { get; set; }

    [StringLength(7)]
    [Unicode(false)]
    public string? ColorHex { get; set; }

    [Precision(3)]
    public DateTime CreatedAt { get; set; }

    [Precision(3)]
    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Series")]
    public virtual ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
}
