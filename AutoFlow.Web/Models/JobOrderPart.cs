using System.ComponentModel.DataAnnotations;

namespace AutoFlow.Web.Models;

public class JobOrderPart
{
    [Key]
    public int JobOrderPartId { get; set; }

    [Required]
    public int JobOrderId { get; set; }

    [Required]
    public int PartId { get; set; }

    [Required]
    public int Quantity { get; set; } = 1;

    // Snapshot of unit price at time of use — protects historical job cost
    // even if the Part's UnitPrice changes later.
    [Required]
    public decimal UnitPriceAtTimeOfUse { get; set; }

    public JobOrder? JobOrder { get; set; }
    public Part? Part { get; set; }
}