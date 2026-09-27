using System.ComponentModel.DataAnnotations;

namespace AutoFlow.Web.Models;

public class Appointment
{
    public int AppointmentId { get; set; }

    [Required]
    public int CustomerId { get; set; }

    [Required]
    public int VehicleId { get; set; }

    [Required]
    public DateTime AppointmentDate { get; set; }

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Scheduled";

    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Customer? Customer { get; set; }

    public Vehicle? Vehicle { get; set; }
}
