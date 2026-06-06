using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Salary
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [Display(Name = "Month")]
        public int Month { get; set; }

        [Display(Name = "Year")]
        public int Year { get; set; }

        [Display(Name = "Basic Salary")]
        [DataType(DataType.Currency)]
        public decimal BasicSalary { get; set; }

        [Display(Name = "Allowances")]
        [DataType(DataType.Currency)]
        public decimal Allowances { get; set; }

        [Display(Name = "Deductions")]
        [DataType(DataType.Currency)]
        public decimal Deductions { get; set; }

        [Display(Name = "Deducted Advances")]
        [DataType(DataType.Currency)]
        public decimal AdvanceDeducted { get; set; }

        [Display(Name = "Employee Debt Deducted")]
        [DataType(DataType.Currency)]
        public decimal EmployeeDebtDeducted { get; set; }

        [Display(Name = "Net")]
        [DataType(DataType.Currency)]
        public decimal NetSalary { get; set; }

        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime? PaidDate { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending";

        [Display(Name = "Invoice Commission")]
        [DataType(DataType.Currency)]
        public decimal CommissionAmount { get; set; }

        [Display(Name = "Total Gifts")]
        [DataType(DataType.Currency)]
        public decimal? GiftAmount { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Cash";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}