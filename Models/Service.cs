using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Service
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الخدمة مطلوب")]
        [Display(Name = "اسم الخدمة")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "السعر مطلوب")]
        [Display(Name = "السعر")]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [Display(Name = "المدة (دقائق)")]
        public int? DurationMinutes { get; set; }

        [Display(Name = "نوع الخدمة")]
        public string? ServiceType { get; set; }

        [Display(Name = "العمولة")]
        [DataType(DataType.Currency)]
        public decimal Commission { get; set; }

        [Display(Name = "الفئة")]
        public int? ServiceCategoryId { get; set; }

        [ForeignKey("ServiceCategoryId")]
        public ServiceCategory? ServiceCategory { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<AppointmentService> AppointmentServices { get; set; } = new List<AppointmentService>();
        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }
}
