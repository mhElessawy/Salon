namespace Salon.Models
{
    public class EmployeeAdvanceSummaryViewModel
    {
        public Employee Employee { get; set; } = null!;
        public List<EmployeeAdvance> Advances { get; set; } = new();
        public List<SalaryAdvanceDeduction> SalaryDeductions { get; set; } = new();

        private IEnumerable<EmployeeAdvance> NonRejectedAdvances =>
            Advances.Where(a => a.Status != EmployeeAdvance.Statuses.Rejected);

        public decimal TotalAmount => NonRejectedAdvances.Sum(a => a.Amount);
        public decimal TotalDeducted => NonRejectedAdvances.Sum(a => a.DeductedAmount);
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
