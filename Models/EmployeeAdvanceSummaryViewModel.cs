namespace Salon.Models
{
    public class EmployeeAdvanceSummaryViewModel
    {
        public Employee Employee { get; set; } = null!;
        public List<EmployeeAdvance> Advances { get; set; } = new();
        public List<SalaryAdvanceDeduction> SalaryDeductions { get; set; } = new();

        public decimal TotalAmount => Advances.Sum(a => a.Amount);
        public decimal TotalDeducted => Advances.Sum(a => a.DeductedAmount);
        public decimal TotalRemaining => TotalAmount - TotalDeducted;
    }

    public class SalaryAdvanceDeduction
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal Amount { get; set; }
        public DateTime? PaidDate { get; set; }
    }
}
