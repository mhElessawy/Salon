using System.ComponentModel.DataAnnotations;

namespace Salon.Models
{
    public class ServiceCategory
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Category name is required")]
        [Display(Name = "Category Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Icon (Font Awesome)")]
        public string? Icon { get; set; } = "fas fa-cut";

        [Display(Name = "Color")]
        public string? Color { get; set; } = "#F7941D";

        [Display(Name = "Department")]
        public string? Department { get; set; } // "Barber" | "Massage"

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Service> Services { get; set; } = new List<Service>();
    }
}
