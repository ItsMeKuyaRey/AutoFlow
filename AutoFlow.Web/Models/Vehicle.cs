using System.ComponentModel.DataAnnotations;

namespace AutoFlow.Web.Models
{
    public class Vehicle
    {
        [Key]
        public int VehicleId { get; set; }
        public int CustomerId { get; set; }

        [Required]
        [MaxLength(20)]
        public string PlateNumber { get; set; } = string.Empty;
        
        [MaxLength(50)]
        public string? VIN { get; set; }

        [Required]
        [MaxLength(50)]
        public string Make { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string Model { get; set; } = string.Empty;

        public int? Year { get; set;}

        [MaxLength(30)]
        public string? Color { get; set; }

        public int? Mileage { get; set;}

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Customer? Customer { get; set; }
    }
}