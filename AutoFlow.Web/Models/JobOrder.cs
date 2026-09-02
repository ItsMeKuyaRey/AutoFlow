using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoFlow.Web.Models;

public class JobOrder
{
    [Key]
    public int JobOrderId { get; set; }

    [Required]
    public int VehicleId { get; set; }

    public int? AppointmentId { get; set; }

    [Required]
    public DateTime JobOrderDate { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Open";

    public decimal LaborCost { get; set; } = 0;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(1000)]
    public string? Diagnosis { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Vehicle? Vehicle { get; set; }

    public Appointment? Appointment { get; set; }

    public ICollection<JobOrderPart> JobOrderParts { get; set; } = new List<JobOrderPart>();

    [NotMapped]
    public decimal PartsCost => JobOrderParts?.Sum(jp => jp.Quantity * jp.UnitPriceAtTimeOfUse) ?? 0;

    [NotMapped]
    public decimal TotalCost => LaborCost + PartsCost;
}