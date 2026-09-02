using System.ComponentModel.DataAnnotations;

namespace AutoFlow.Web.Models;

public class Appointment
{
    public int AppointmentId { get; set; }

    [Required]
    public int VehicleId { get; set; }

    public DateTime AppointmentDate { get; set; }

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Scheduled";

    [StringLength(500)]
    public string? CustomerConcern { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Vehicle? Vehicle { get; set; }
}