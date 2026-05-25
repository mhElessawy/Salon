using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class ServicePackage
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الباقة (عربي) مطلوب")]
        [Display(Name = "اسم الباقة (عربي)")]
        public string NameAr { get; set; } = string.Empty;

        [Display(Name = "اسم الباقة (English)")]
        public string? NameEn { get; set; }

        [Required(ErrorMessage = "عدد الجلسات مطلوب")]
        [Display(Name = "عدد الجلسات")]
        [Range(1, 1000, ErrorMessage = "عدد الجلسات يجب أن يكون بين 1 و 1000")]
        public int SessionCount { get; set; } = 4;

        [Required(ErrorMessage = "السعر مطلوب")]
        [Display(Name = "السعر")]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [Display(Name = "مدة الصلاحية (أيام)")]
        [Range(1, 3650, ErrorMessage = "مدة الصلاحية يجب أن تكون بين 1 و 3650 يوم")]
        public int ValidityDays { get; set; } = 90;

        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Display(Name = "مفعّل")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "فئة الخدمة")]
        public int? ServiceCategoryId { get; set; }

        [ForeignKey("ServiceCategoryId")]
        public ServiceCategory? ServiceCategory { get; set; }

        public ICollection<CustomerPackage> CustomerPackages { get; set; } = new List<CustomerPackage>();
    }
}
