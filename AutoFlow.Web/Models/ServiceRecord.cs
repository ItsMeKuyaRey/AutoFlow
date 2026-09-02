using System.ComponentModel.DataAnnotations;

namespace AutoFlow.Web.Models
{
    public class ServiceRecord
    {
        [Key]
        public int ServiceRecordId { get; set; }

        [Required]
        public int VehicleId { get; set; }

        [Required]
        public DateTime ServiceDate { get; set; } = DateTime.UtcNow;

        public int? Mileage { get; set; }

        [MaxLength(500)]
        public string? Complaint { get; set; }

        [MaxLength(1000)]
        public string? Diagnosis { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Vehicle? Vehicle { get; set; }
    }
}
