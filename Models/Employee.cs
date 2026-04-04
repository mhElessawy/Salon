using System.ComponentModel.DataAnnotations;

namespace Salon.Models
{
    public enum ServiceType
    {
        Haircut,
        Massage,
        Both
    }

    public class Employee
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الاسم مطلوب")]
        [Display(Name = "الاسم")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Display(Name = "رقم الهاتف")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "العمولة مطلوبة")]
        [Range(0, 100, ErrorMessage = "العمولة يجب أن تكون بين 0 و 100")]
        [Display(Name = "العمولة (%)")]
        public decimal Commission { get; set; }

        [Required(ErrorMessage = "نوع الخدمة مطلوب")]
        [Display(Name = "نوع الخدمة")]
        public ServiceType ServiceType { get; set; }
    }
}
