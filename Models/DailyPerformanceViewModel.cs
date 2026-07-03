namespace Salon.Models
{
    public class EmployeePerformanceRow
    {
        public Employee Employee { get; set; } = null!;
        public string DepartmentName => Employee.DepartmentNav?.Name ?? "";
        public int InvoiceCount { get; set; }
        public decimal TotalSales { get; set; }
        public decimal InstantCollection { get; set; }
        public decimal Cash { get; set; }
        public decimal KNet { get; set; }
        public decimal Debts { get; set; }
        public decimal Advances { get; set; }
        public decimal SalesPercent { get; set; }
        public decimal CommissionPercent { get; set; }
        public decimal DueAmount { get; set; }
        public decimal ShopNet { get; set; }
        public decimal Hadiya { get; set; }
    }

    public class DailyPerformanceViewModel
    {
        public DateTime ReportDate { get; set; }
        public string DayName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string ReportTime { get; set; } = string.Empty;
        public string ReportNumber { get; set; } = string.Empty;
        public string? UserDepartment { get; set; }
        public string? SelectedDept { get; set; }

        // Revenue summary
        public decimal TotalSales { get; set; }
        public int InvoiceCount { get; set; }
        public decimal CashTotal { get; set; }
        public decimal KNetTotal { get; set; }
        public decimal DebtTotal { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal TipsTotal { get; set; }
        public decimal TipsDelivered { get; set; }
        public decimal TipsRemaining => TipsTotal - TipsDelivered;

        public decimal CashPercent => TotalSales > 0 ? Math.Round(CashTotal / TotalSales * 100, 1) : 0;
        public decimal KNetPercent => TotalSales > 0 ? Math.Round(KNetTotal / TotalSales * 100, 1) : 0;
        public decimal DebtPercent => TotalSales > 0 ? Math.Round(DebtTotal / TotalSales * 100, 1) : 0;
        public decimal GrossTotal => TotalSales + TotalDiscount;
        public decimal DiscountPercent => GrossTotal > 0 ? Math.Round(TotalDiscount / GrossTotal * 100, 1) : 0;

        // Cash register
        public decimal OpeningBalance { get; set; }
        public decimal CashRevenue { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalExpensesAmount { get; set; }
        public decimal TotalAdvancesAmount { get; set; }
        public decimal TotalWithdrawals { get; set; }
        // Only cash-paid expenses/advances actually leave the physical register — expenses or
        // advances paid by card/transfer must not reduce the cash balance.
        public decimal CashExpensesAmount { get; set; }
        public decimal CashAdvancesAmount { get; set; }
        public decimal CurrentCashBalance => OpeningBalance + CashRevenue + TotalDeposits
                                             - CashExpensesAmount - CashAdvancesAmount - TotalWithdrawals;

        // Expenses analytics
        public int ExpenseCount { get; set; }
        public decimal MaxExpense { get; set; }
        public decimal AvgExpense => ExpenseCount > 0 ? Math.Round(TotalExpensesAmount / ExpenseCount, 3) : 0;
        public Dictionary<string, decimal> ExpensesByCategory { get; set; } = new();

        // Employee rows
        public List<EmployeePerformanceRow> BarberRows { get; set; } = new();
        public List<EmployeePerformanceRow> MassageRows { get; set; } = new();
        public List<EmployeePerformanceRow> EmployeeRows => BarberRows.Concat(MassageRows).ToList();

        public int WorkHours { get; set; }
        public decimal AvgInvoiceValue => InvoiceCount > 0 ? Math.Round(TotalSales / InvoiceCount, 3) : 0;

        // Tab data
        public List<Sale> Invoices { get; set; } = new();
        public List<Expense> Expenses { get; set; } = new();
        public List<EmployeeAdvance> Advances { get; set; } = new();
        public List<Sale> TipInvoices { get; set; } = new();

        public string DailyNotes { get; set; } = string.Empty;
        public int ShiftId { get; set; }

        public DateTime PrevDate => ReportDate.AddDays(-1);
        public DateTime NextDate => ReportDate.AddDays(1);
    }
}