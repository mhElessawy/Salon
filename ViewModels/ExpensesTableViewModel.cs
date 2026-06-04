using Salon.Models;

namespace Salon.ViewModels
{
    public class ExpensesTableViewModel
    {
        public List<Expense> Expenses { get; set; } = new();
        public List<Salary>  Salaries { get; set; } = new();
        public decimal Total   { get; set; }
        public bool    ShowDept { get; set; }
    }
}
