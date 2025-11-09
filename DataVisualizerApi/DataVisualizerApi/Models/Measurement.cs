using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataVisualizerApi.Models;

[Index("SeriesId", "MeasuredAt", Name = "IX_Measurements_Series_Time")]
public partial class Measurement
{
    [Key]
    public long MeasurementId { get; set; }

    public int SeriesId { get; set; }

    [Precision(3)]
    public DateTime MeasuredAt { get; set; }

    public double Value { get; set; }

    [Precision(3)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("SeriesId")]
    [InverseProperty("Measurements")]
    public virtual Series Series { get; set; } = null!;
}
