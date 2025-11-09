using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataVisualizerApi.Models;

[Index("EmailHash", Name = "IX_Users_EmailHash")]
[Index("EmailHash", Name = "UQ_Users_EmailHash", IsUnique = true)]
public partial class User
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(32)]
    public byte[] EmailHash { get; set; } = null!;

    public byte EmailHashVersion { get; set; }

    [StringLength(512)]
    public string PasswordHash { get; set; } = null!;

    [Precision(3)]
    public DateTime CreatedAt { get; set; }

    [Precision(3)]
    public DateTime UpdatedAt { get; set; }
}
