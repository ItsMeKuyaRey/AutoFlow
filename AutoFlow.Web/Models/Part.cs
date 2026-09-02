using System.ComponentModel.DataAnnotations;

namespace AutoFlow.Web.Models;

public class Part
{
    [Key]
    public int PartId { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string? PartNumber { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    [Required]
    public int StockQuantity { get; set; } = 0;

    // Nullable on purpose — Supplier module (Phase 7) doesn't exist yet.
    // We'll add the FK/navigation when Supplier is built, not before.
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}