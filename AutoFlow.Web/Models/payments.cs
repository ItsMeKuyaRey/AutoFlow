using System.ComponentModel.DataAnnotations;

namespace AutoFlow.Web.Models;

public class Payment
{
    [Key]
    public int PaymentId { get; set; }

    [Required]
    public int BillingId { get; set; }

    [Required]
    public decimal AmountPaid { get; set; }

    [Required]
    [StringLength(30)]
    public string PaymentMethod { get; set; } = "Cash";

    [Required]
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Billing? Billing { get; set; }
}