namespace Salon.Models
{
    /// <summary>
    /// بيانات شاشة "مراجعة واعتماد اليومية" — تجميع كل البنود المطلوب مراجعتها قبل الاعتماد.
    /// </summary>
    public class DailyClosureViewModel
    {
        public Shift Shift { get; set; } = null!;
        public DateTime Date { get; set; }

        /// <summary>القسم المعروض حالياً: حلاقة/مساج/عام.</summary>
        public string Department { get; set; } = Shift.ClosureDepartments.Shared;

        /// <summary>true للأدمن/المدير — يظهر لهم فلتر اختيار القسم. الكاشير يشوف قسمه فقط بدون فلتر.</summary>
        public bool CanPickDepartment { get; set; }

        /// <summary>الأقسام المتاحة للفلتر (للأدمن/المدير فقط).</summary>
        public List<string> AvailableDepartments { get; set; } = new();

        public decimal TotalRevenue { get; set; }
        public decimal TotalCash { get; set; }
        public decimal SystemKnetTotal { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalWithdrawals { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalAdvancesToday { get; set; }
        public decimal OutstandingEmployeeDebts { get; set; }
        public decimal CustodyRemaining { get; set; }

        public decimal ExpectedCashBalance { get; set; }

        public bool IsLocked { get; set; }
        public bool CanApprove { get; set; }
        public bool CanReopen { get; set; }

        /// <summary>تاريخ يومية سابقة أُغلقت آلياً بدون اعتماد (غير اليومية المعروضة حالياً)، لتنبيه المستخدم بها فور الدخول.</summary>
        public DateTime? PendingApprovalDate { get; set; }

        public List<Expense> Expenses { get; set; } = new();
        public List<Withdrawal> Withdrawals { get; set; } = new();
        public List<Deposit> Deposits { get; set; } = new();
        public List<EmployeeAdvance> Advances { get; set; } = new();
        public List<Custody> Custodies { get; set; } = new();
    }
}