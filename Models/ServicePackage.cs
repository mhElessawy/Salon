using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class ServicePackage
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Package name (Arabic) is required")]
        [Display(Name = "Package Name (Arabic)")]
        public string NameAr { get; set; } = string.Empty;

        [Display(Name = "Package Name (English)")]
        public string? NameEn { get; set; }

        [Required(ErrorMessage = "Session count is required")]
        [Display(Name = "Session Count")]
        [Range(1, 1000, ErrorMessage = "Session count must be between 1 and 1000")]
        public int SessionCount { get; set; } = 4;

        [Required(ErrorMessage = "Price is required")]
        [Display(Name = "Price")]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [Display(Name = "Validity Duration (Days)")]
        [Range(1, 3650, ErrorMessage = "Validity must be between 1 and 3650 days")]
        public int ValidityDays { get; set; } = 90;

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Service Category")]
        public int? ServiceCategoryId { get; set; }

        [ForeignKey("ServiceCategoryId")]
        public ServiceCategory? ServiceCategory { get; set; }

        public ICollection<CustomerPackage> CustomerPackages { get; set; } = new List<CustomerPackage>();
    }
}