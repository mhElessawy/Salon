namespace Salon.Models
{
    public class EmployeeAdvanceSummaryViewModel
    {
        public Employee Employee { get; set; } = null!;
        public List<EmployeeAdvance> Advances { get; set; } = new();
        public List<SalaryAdvanceDeduction> SalaryDeductions { get; set; } = new();

        private IEnumerable<EmployeeAdvance> CountedAdvances =>
            Advances.Where(a => a.Status != EmployeeAdvance.Statuses.Rejected
                && a.Status != EmployeeAdvance.Statuses.Cancelled);

        public decimal TotalAmount => CountedAdvances.Sum(a => a.Amount);
        public decimal TotalDeducted => CountedAdvances.Sum(a => a.DeductedAmount);
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
