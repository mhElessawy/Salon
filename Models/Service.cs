using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Service
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Service name is required")]
        [Display(Name = "Service Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Display(Name = "Price")]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [Display(Name = "Duration (Minutes)")]
        public int? DurationMinutes { get; set; }

        [Display(Name = "Category")]
        public int? ServiceCategoryId { get; set; }

        [ForeignKey("ServiceCategoryId")]
        public ServiceCategory? ServiceCategory { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<AppointmentService> AppointmentServices { get; set; } = new List<AppointmentService>();
        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }
}
