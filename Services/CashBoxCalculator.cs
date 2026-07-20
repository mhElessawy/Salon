using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

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
    /// الصندوق (Reports/CashMovement) واعتماد اليومية (DailyClosure)، حتى لا يفترق حساب أي منهم عن
    /// بعض مرة أخرى. أي تعديل على قواعد حساب الصندوق (نوع فاتورة، قسم، طريقة دفع...) يكفي أن يحدث هنا فقط.
    /// </summary>
    public static class CashBoxCalculator
    {
        /// <param name="dept">
        /// "حلاقة"/"مساج" لحصر الحساب على قسم إيرادي محدد، أو null/فاضي للعرض الكامل (كل الصندوق).
        /// يُتجاهَل لو sharedOnly = true.
        /// </param>
        /// <param name="sharedOnly">
        /// true لحصر الحساب على البنود اللي مالهاش قسم إيرادي محدد (حلاقة/مساج) — مبيعات المنتجات،
        /// المصروفات/السحوبات/الإيداعات المشتركة، وسلف/رواتب موظفي الأقسام غير الإيرادية. يُستخدم في
        /// نطاق "عام" باعتماد اليومية.
        /// </param>
        public static async Task<CashBoxSnapshot> GetSnapshotAsync(
            ApplicationDbContext context, DateTime periodFrom, DateTime periodToExclusive, string? dept, bool sharedOnly = false)
        {
            bool filterDept = sharedOnly || !string.IsNullOrEmpty(dept);

            // ملحوظة: هنا مقصود عدم استبعاد صفوف IsClosureRecord — الهدف من هذا الاستعلام تحديد
            // أول تاريخ عندنا فيه بيانات مسجَّلة أصلاً (لترحيل رصيد الصندوق تراكمياً من قبلها)،
            // مش قراءة رصيد افتتاحي يدوي حقيقي؛ فلو المحل ما استخدمش شاشة "الشفتات" اليدوية
            // إطلاقاً واعتمد بس على شاشة اعتماد اليومية، استبعاد صفوف الإغلاق كان بيخلي هذا
            // الاستعلام يرجع فاضي دايماً فيتصفّر رصيد ما قبل الفترة بالغلط.
            var firstShiftEver = await context.Shifts
                .OrderBy(s => s.ShiftDate).ThenBy(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            bool hasBaseline = firstShiftEver != null && firstShiftEver.ShiftDate.Date <= periodFrom;
            DateTime baseDate = hasBaseline ? firstShiftEver!.ShiftDate.Date : periodFrom;

            // العدّ اليدوي الفعلي للصندوق المشترك بالكامل ليس له تقسيم بين الأقسام، فلا يُحسب
            // إلا في العرض الكامل (بدون فلتر قسم) — تماماً كما في كل تقارير الصندوق الأخرى.
            decimal baseBalance = filterDept ? 0m : (hasBaseline ? firstShiftEver!.OpeningBalance : 0m);

            var prior = await ComputeFlowsAsync(context, baseDate, periodFrom, dept, filterDept, sharedOnly);
            decimal openingBalance = baseBalance + prior.CashRevenue + prior.Deposits
                - prior.CashExpenses - prior.CashAdvances - prior.CashSalaries - prior.Withdrawals;

            var current = await ComputeFlowsAsync(context, periodFrom, periodToExclusive, dept, filterDept, sharedOnly);

            return new CashBoxSnapshot(openingBalance, current.CashRevenue, current.Deposits,
                current.CashExpenses, current.CashAdvances, current.CashSalaries, current.Withdrawals);
        }

        private record Flows(decimal CashRevenue, decimal Deposits, decimal CashExpenses,
            decimal CashAdvances, decimal CashSalaries, decimal Withdrawals);

        private static async Task<Flows> ComputeFlowsAsync(
            ApplicationDbContext context, DateTime from, DateTime to, string? dept, bool filterDept, bool sharedOnly)
        {
            // كل أنواع الفواتير (حلاقة/مساج/منتجات) تدخل الصندوق الفعلي، وليس فواتير الموظفين
            // فقط — الصندوق واحد للمحل بالكامل.
            var salesQuery = context.Sales.Where(s => s.SaleDate >= from && s.SaleDate < to && s.Status != "ملغي");
            if (sharedOnly) salesQuery = salesQuery.Where(s => s.SaleType != "حلاقة" && s.SaleType != "مساج");
            else if (filterDept) salesQuery = salesQuery.Where(s => s.SaleType == dept);
            var sales = await salesQuery.ToListAsync();
            decimal cashRevenue = sales.Sum(s =>
                s.PaymentMethod == "كاش" ? s.NetAmount :
                s.PaymentMethod == "كي نت و كاش" ? (s.CashAmount ?? 0) : 0m);

            // الإيداعات النقدية فقط تدخل الصندوق الفعلي — إيداع بالتحويل البنكي أو البطاقة
            // ميعديش على الكاش الفعلي في الدرج، فمينفعش يزود رصيد الكاش.
            var depositsQuery = context.Deposits.Where(d => d.DepositDate >= from && d.DepositDate < to && d.PaymentMethod == "نقدي");
            if (sharedOnly) depositsQuery = depositsQuery.Where(d => d.Department != "حلاقة" && d.Department != "مساج");
            else if (filterDept) depositsQuery = depositsQuery.Where(d => d.Department == dept);
            decimal deposits = (await depositsQuery.ToListAsync()).Sum(d => d.Amount);

            // فئة "عهدة" مستبعدة هنا لأن العهدة لا تؤثر على الصندوق إطلاقاً — هي مبلغ منفصل تحت
            // عهدة الموظف، مش مصروف فعلي خرج من الكاش.
            var expQuery = context.Expenses.Where(e => e.ExpenseDate >= from && e.ExpenseDate < to
                     && e.PaymentMethod == "نقدي" && e.Category != "عهدة");
            if (sharedOnly) expQuery = expQuery.Where(e => e.Department != "حلاقة" && e.Department != "مساج");
            else if (filterDept) expQuery = expQuery.Where(e => e.Department == dept);
            decimal cashExpenses = (await expQuery.ToListAsync()).Sum(e => e.Amount);

            // القسم "الفعلي" للموظف يُحسب حسب RevenueDepartment إن وُجد (لموظفي الأقسام غير
            // الإيرادية كالإدارة)، وإلا فقسمه التنظيمي (DepartmentNav) — يطابق نفس المنطق
            // المستخدم في تقرير الأرباح/الإيرادات حتى تظهر سلف ورواتب الإداريين التابعين لقسم
            // حلاقة/مساج تحت نفس القسم مش بس موظفيه المباشرين.
            var advQuery = context.EmployeeAdvances.Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .Where(a => a.AdvanceDate >= from && a.AdvanceDate < to
                         && EmployeeAdvance.Statuses.Realized.Contains(a.Status) && a.PaymentMethod == "نقدي");
            if (sharedOnly) advQuery = advQuery.Where(a => (a.Employee!.RevenueDepartment ?? a.Employee!.DepartmentNav!.Name) != "حلاقة"
                     && (a.Employee!.RevenueDepartment ?? a.Employee!.DepartmentNav!.Name) != "مساج");
            else if (filterDept) advQuery = advQuery.Where(a => (a.Employee!.RevenueDepartment ?? a.Employee!.DepartmentNav!.Name) == dept);
            decimal cashAdvances = (await advQuery.ToListAsync()).Sum(a => a.Amount);

            var salQuery = context.Salaries.Include(s => s.Employee).ThenInclude(e => e!.DepartmentNav)
                .Where(s => s.PaidDate.HasValue && s.PaidDate.Value >= from && s.PaidDate.Value < to && s.PaymentMethod == "نقدي");
            if (sharedOnly) salQuery = salQuery.Where(s => (s.Employee!.RevenueDepartment ?? s.Employee!.DepartmentNav!.Name) != "حلاقة"
                     && (s.Employee!.RevenueDepartment ?? s.Employee!.DepartmentNav!.Name) != "مساج");
            else if (filterDept) salQuery = salQuery.Where(s => (s.Employee!.RevenueDepartment ?? s.Employee!.DepartmentNav!.Name) == dept);
            decimal cashSalaries = (await salQuery.ToListAsync()).Sum(s => s.NetSalary);

            var wdQuery = context.Withdrawals.Where(w => w.WithdrawalDate >= from && w.WithdrawalDate < to);
            if (sharedOnly) wdQuery = wdQuery.Where(w => w.Department != "حلاقة" && w.Department != "مساج");
            else if (filterDept) wdQuery = wdQuery.Where(w => w.Department == dept);
            decimal withdrawals = (await wdQuery.ToListAsync()).Sum(w => w.Amount);

            return new Flows(cashRevenue, deposits, cashExpenses, cashAdvances, cashSalaries, withdrawals);
        }
    }
}
