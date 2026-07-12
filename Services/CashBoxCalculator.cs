using Microsoft.EntityFrameworkCore;
using Salon.Data;

namespace Salon.Services
{
    // نتيجة حساب حركة الصندوق لفترة معيّنة
    public record CashBoxSnapshot(
        decimal OpeningBalance,
        decimal CashRevenue,
        decimal Deposits,
        decimal CashExpenses,
        decimal CashAdvances,
        decimal CashSalaries,
        decimal Withdrawals)
    {
        public decimal ClosingBalance => OpeningBalance + CashRevenue + Deposits
            - CashExpenses - CashAdvances - CashSalaries - Withdrawals;
    }

    /// <summary>
    /// معادلة موحّدة لحركة الصندوق يستخدمها كل من تقرير الأداء اليومي (BarberDaily) وتقرير حركة
    /// الصندوق (Reports/CashMovement)، حتى لا يفترق حساب الاثنين عن بعض مرة أخرى. أي تعديل على
    /// قواعد حساب الصندوق (نوع فاتورة، قسم، طريقة دفع...) يكفي أن يحدث هنا فقط.
    /// </summary>
    public static class CashBoxCalculator
    {
        public static async Task<CashBoxSnapshot> GetSnapshotAsync(
            ApplicationDbContext context, DateTime periodFrom, DateTime periodToExclusive, string? dept)
        {
            bool filterDept = !string.IsNullOrEmpty(dept);

            var firstShiftEver = await context.Shifts
                .OrderBy(s => s.ShiftDate).ThenBy(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            bool hasBaseline = firstShiftEver != null && firstShiftEver.ShiftDate.Date <= periodFrom;
            DateTime baseDate = hasBaseline ? firstShiftEver!.ShiftDate.Date : periodFrom;

            // العدّ اليدوي الفعلي للصندوق المشترك بالكامل ليس له تقسيم بين الأقسام، فلا يُحسب
            // إلا في العرض الكامل (بدون فلتر قسم) — تماماً كما في كل تقارير الصندوق الأخرى.
            decimal baseBalance = filterDept ? 0m : (hasBaseline ? firstShiftEver!.OpeningBalance : 0m);

            var prior = await ComputeFlowsAsync(context, baseDate, periodFrom, dept, filterDept);
            decimal openingBalance = baseBalance + prior.CashRevenue + prior.Deposits
                - prior.CashExpenses - prior.CashAdvances - prior.CashSalaries - prior.Withdrawals;

            var current = await ComputeFlowsAsync(context, periodFrom, periodToExclusive, dept, filterDept);

            return new CashBoxSnapshot(openingBalance, current.CashRevenue, current.Deposits,
                current.CashExpenses, current.CashAdvances, current.CashSalaries, current.Withdrawals);
        }

        private record Flows(decimal CashRevenue, decimal Deposits, decimal CashExpenses,
            decimal CashAdvances, decimal CashSalaries, decimal Withdrawals);

        private static async Task<Flows> ComputeFlowsAsync(
            ApplicationDbContext context, DateTime from, DateTime to, string? dept, bool filterDept)
        {
            // كل أنواع الفواتير (حلاقة/مساج/منتجات) تدخل الصندوق الفعلي، وليس فواتير الموظفين
            // فقط — الصندوق واحد للمحل بالكامل.
            var salesQuery = context.Sales.Where(s => s.SaleDate >= from && s.SaleDate < to && s.Status != "ملغي");
            if (filterDept) salesQuery = salesQuery.Where(s => s.SaleType == dept);
            var sales = await salesQuery.ToListAsync();
            decimal cashRevenue = sales.Sum(s =>
                s.PaymentMethod == "كاش" ? s.NetAmount :
                s.PaymentMethod == "كي نت و كاش" ? (s.CashAmount ?? 0) : 0m);

            var depositsQuery = context.Deposits.Where(d => d.DepositDate >= from && d.DepositDate < to);
            if (filterDept) depositsQuery = depositsQuery.Where(d => d.Department == dept);
            decimal deposits = await depositsQuery.SumAsync(d => d.Amount);

            // فئة "عهدة" مستبعدة هنا لأن العهدة لا تؤثر على الصندوق إطلاقاً — هي مبلغ منفصل تحت
            // عهدة الموظف، مش مصروف فعلي خرج من الكاش.
            var expQuery = context.Expenses.Where(e => e.ExpenseDate >= from && e.ExpenseDate < to
                     && e.PaymentMethod == "نقدي" && e.Category != "عهدة");
            if (filterDept) expQuery = expQuery.Where(e => e.Department == dept);
            decimal cashExpenses = await expQuery.SumAsync(e => e.Amount);

            // كل السلف/الرواتب النقدية المصروفة تُخصم من الصندوق مهما كان قسم الموظف — الفلترة
            // بالقسم تُطبَّق فقط عند اختيار قسم معيّن، وليس لموظفي حلاقة/مساج فقط.
            var advQuery = context.EmployeeAdvances.Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .Where(a => a.AdvanceDate >= from && a.AdvanceDate < to
                         && (a.Status == "موافق عليها" || a.Status == "مسددة") && a.PaymentMethod == "نقدي");
            if (filterDept) advQuery = advQuery.Where(a => a.Employee!.DepartmentNav!.Name == dept);
            decimal cashAdvances = await advQuery.SumAsync(a => a.Amount);

            var salQuery = context.Salaries.Include(s => s.Employee).ThenInclude(e => e!.DepartmentNav)
                .Where(s => s.PaidDate.HasValue && s.PaidDate.Value >= from && s.PaidDate.Value < to && s.PaymentMethod == "نقدي");
            if (filterDept) salQuery = salQuery.Where(s => s.Employee!.DepartmentNav!.Name == dept);
            decimal cashSalaries = await salQuery.SumAsync(s => s.NetSalary);

            var wdQuery = context.Withdrawals.Where(w => w.WithdrawalDate >= from && w.WithdrawalDate < to);
            if (filterDept) wdQuery = wdQuery.Where(w => w.Department == dept);
            decimal withdrawals = await wdQuery.SumAsync(w => w.Amount);

            return new Flows(cashRevenue, deposits, cashExpenses, cashAdvances, cashSalaries, withdrawals);
        }
    }
}
