using System.ComponentModel.DataAnnotations;

namespace AutoFlow.Web.Models;

public class CustomerFollowUp
{
    [Key]
    public int FollowUpId { get; set; }

    [Required]
    public int CustomerId { get; set; }

    [Required]
    public int VehicleId { get; set; }

    public int? ServiceRecordId { get; set; }

    [Required]
    public DateTime FollowUpDate { get; set; }

    [Required]
    [StringLength(50)]
    public string FollowUpType { get; set; } = "Service Follow-up";

    [Required]
    [StringLength(150)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Pending";

    public DateTime? NextFollowUpDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Customer? Customer { get; set; }

    public Vehicle? Vehicle { get; set; }

    public ServiceRecord? ServiceRecord { get; set; }
}