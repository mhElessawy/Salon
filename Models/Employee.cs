using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Employee
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        // Alias for backward compatibility
        [Display(Name = "Name")]
        public string? Name { get; set; }

        [Display(Name = "Commission (%)")]
        public decimal Commission { get; set; }

        [Display(Name = "Phone Number")]
        public string? Phone { get; set; }

        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Job Title")]
        public string? JobTitle { get; set; }

        [Display(Name = "Basic Salary")]
        [DataType(DataType.Currency)]
        public decimal BasicSalary { get; set; }

        [Display(Name = "Hire Date")]
        [DataType(DataType.Date)]
        public DateTime HireDate { get; set; } = DateTime.Today;

        [Display(Name = "Birth Date")]
        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }

        [Display(Name = "Nationality")]
        public string? Nationality { get; set; }

        [Display(Name = "Residency / ID Number")]
        public string? IdNumber { get; set; }

        [Display(Name = "Residency Expiry Date")]
        [DataType(DataType.Date)]
        public DateTime? ResidencyExpiry { get; set; }

        [Display(Name = "Contract Type")]
        public string? ContractType { get; set; }

        [Display(Name = "Department")]
        public int? DepartmentId { get; set; }

        [ForeignKey("DepartmentId")]
        public Department? DepartmentNav { get; set; }

        // Kept for backward compatibility — read from DepartmentNav
        [NotMapped]
        public string? Department => DepartmentNav?.Name;

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
        public ICollection<Salary> Salaries { get; set; } = new List<Salary>();
        public ICollection<EmployeeAdvance> Advances { get; set; } = new List<EmployeeAdvance>();
    }
}