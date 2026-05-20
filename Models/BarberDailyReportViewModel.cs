namespace Salon.Models
{
    public class BarberDailyRow
    {
        public Employee Employee { get; set; } = null!;
        public decimal TotalWork { get; set; }
        public decimal KNet { get; set; }
        public decimal Cash { get; set; }
        public decimal Debts { get; set; }
        public decimal Advance { get; set; }
        public decimal CommissionPercent { get; set; }
        public decimal DueAmount { get; set; }
        public decimal Deductions { get; set; }
        public decimal NetAfterDeduction { get; set; }
        public decimal ShopNet { get; set; }
    }

    public class CashMovementRow
    {
        public DateTime Date { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal CashRevenue { get; set; }
        public decimal Withdrawal { get; set; }
        public string WithdrawalType { get; set; } = string.Empty;
        public string WithdrawnBy { get; set; } = string.Empty;
        public decimal ClosingBalance { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class BarberDailyReportViewModel
    {
        public DateTime ReportDate { get; set; }
        public string ReportNumber { get; set; } = string.Empty;
        public string DayName { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public string ClosingTime { get; set; } = string.Empty;

        public decimal TotalRevenue { get; set; }
        public decimal TotalKNet { get; set; }
        public decimal TotalCash { get; set; }
        public decimal ProductSalesTotal { get; set; }
        public decimal NetShopIncome { get; set; }

        public int RegisteredBarbers { get; set; }
        public int PresentToday { get; set; }
        public int AbsentToday { get; set; }
        public int VacationToday { get; set; }
        public int LateToday { get; set; }
        public int EarlyLeaveToday { get; set; }

        public List<BarberDailyRow> BarberRows { get; set; } = new();

        public decimal TotalWork => BarberRows.Sum(r => r.TotalWork);
        public decimal TotalKNetWork => BarberRows.Sum(r => r.KNet);
        public decimal TotalCashWork => BarberRows.Sum(r => r.Cash);
        public decimal TotalDebts => BarberRows.Sum(r => r.Debts);
        public decimal TotalAdvances => BarberRows.Sum(r => r.Advance);
        public decimal OverallCommissionPercent => TotalWork > 0
            ? Math.Round(BarberRows.Sum(r => r.DueAmount) / TotalWork * 100, 1)
            : 0;
        public decimal TotalDueAmount => BarberRows.Sum(r => r.DueAmount);
        public decimal TotalDeductions => BarberRows.Sum(r => r.Deductions);
        public decimal TotalNetAfterDeduction => BarberRows.Sum(r => r.NetAfterDeduction);
        public decimal TotalShopNet => BarberRows.Sum(r => r.ShopNet);

        public List<CashMovementRow> CashMovement { get; set; } = new();
        public decimal CurrentCashBalance => CashMovement.LastOrDefault()?.ClosingBalance ?? 0;
        public DateTime MonthStart { get; set; }
    }
}
