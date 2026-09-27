using System.ComponentModel.DataAnnotations;

namespace AutoFlow.Web.Models;

public class Supplier
{
    [Key]
    public int SupplierId { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ContactPerson { get; set; }

    [Phone]
    [StringLength(30)]
    public string? Phone { get; set; }

    [EmailAddress]
    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [StringLength(50)]
    public string? Status { get; set; } = "Active";   // Active / Inactive

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Part> Parts { get; set; } = new List<Part>();
}
