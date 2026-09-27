using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoFlow.Web.Models;

public class Billing
{
    [Key]
    public int BillingId { get; set; }

    [Required]
    public int JobOrderId { get; set; }

    [Required]
    [StringLength(30)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required]
    public decimal LaborCost { get; set; }

    [Required]
    public decimal PartsCost { get; set; }

    [Required]
    public decimal DiscountAmount { get; set; } = 0;

    [Required]
    public decimal TaxAmount { get; set; } = 0;

    [Required]
    public decimal TotalAmount { get; set; }

    [Required]
    public decimal AmountPaid { get; set; } = 0;

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Unpaid";

    public bool IsArchived { get; set; } = false;

    [Required]
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public JobOrder? JobOrder { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}