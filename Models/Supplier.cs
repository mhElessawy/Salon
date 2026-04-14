using System.ComponentModel.DataAnnotations;

namespace Salon.Models
{
    public class Supplier
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المورد مطلوب")]
        [Display(Name = "اسم المورد")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "رقم الهاتف")]
        public string? Phone { get; set; }

        [Display(Name = "البريد الإلكتروني")]
        public string? Email { get; set; }

        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [Display(Name = "الشخص المسؤول")]
        public string? ContactPerson { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}