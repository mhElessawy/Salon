using System.ComponentModel.DataAnnotations;

namespace Salon.Models
{
    public class Employee
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الاسم مطلوب")]
        [Display(Name = "الاسم الكامل")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "رقم الهاتف")]
        public string? Phone { get; set; }

        [Display(Name = "البريد الإلكتروني")]
        public string? Email { get; set; }

        [Display(Name = "المسمى الوظيفي")]
        public string? JobTitle { get; set; }

        [Display(Name = "الراتب الأساسي")]
        [DataType(DataType.Currency)]
        public decimal BasicSalary { get; set; }

        [Display(Name = "تاريخ التعيين")]
        [DataType(DataType.Date)]
        public DateTime HireDate { get; set; } = DateTime.Today;

        [Display(Name = "تاريخ الميلاد")]
        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }

        [Display(Name = "الجنسية")]
        public string? Nationality { get; set; }

        [Display(Name = "رقم الإقامة / الهوية")]
        public string? IdNumber { get; set; }

        [Display(Name = "تاريخ انتهاء الإقامة")]
        [DataType(DataType.Date)]
        public DateTime? ResidencyExpiry { get; set; }

        [Display(Name = "نوع العقد")]
        public string? ContractType { get; set; }

        [Display(Name = "القسم")]
        public string? Department { get; set; } // "حلاقة" | "مساج"

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
        public ICollection<Salary> Salaries { get; set; } = new List<Salary>();
        public ICollection<EmployeeAdvance> Advances { get; set; } = new List<EmployeeAdvance>();
    }
}
