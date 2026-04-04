using System.ComponentModel.DataAnnotations;

namespace Salon.Models
{
    public class ServiceCategory
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الفئة مطلوب")]
        [Display(Name = "اسم الفئة")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Display(Name = "الأيقونة (Font Awesome)")]
        public string? Icon { get; set; } = "fas fa-cut";

        [Display(Name = "اللون")]
        public string? Color { get; set; } = "#F7941D";

        [Display(Name = "القسم")]
        public string? Department { get; set; } // "حلاقة" | "مساج"

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Service> Services { get; set; } = new List<Service>();
    }
}