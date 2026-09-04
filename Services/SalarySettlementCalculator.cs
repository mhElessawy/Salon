using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Services
{
    // نتيجة تسوية راتب موظف عن شهر معيّن — مصدر واحد للحساب يُستخدم في معاينة الشاشة
    // (GetEmployeeInfo) وفي الحفظ الفعلي (Create) حتى لا يفترق الاثنان عن بعض.
    public class SalarySettlementResult
    {
        // ─── الاستحقاقات ───
        public decimal BasicSalary { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal Allowances { get; set; }
        public decimal GiftAmount { get; set; }   // إجمالي خدمات الموظف من الفواتير
        public decimal HadiyaAmount { get; set; } // إكرامية الموظف من الفواتير
        public decimal TotalEntitlements { get; set; }

        // ─── الخصومات والالتزامات ───
        public decimal Deductions { get; set; }
        public decimal EmployeeDebtTotal { get; set; }
        public decimal CarriedAdvanceBalance { get; set; }
        public decimal NewAdvancesAmount { get; set; }
        public decimal TotalAdvanceDue { get; set; }
        public decimal AvailableForAdvanceRepayment { get; set; }
        public decimal AdvanceDeducted { get; set; }
        public decimal RemainingAdvanceCarried { get; set; }

        // ─── التسوية النهائية ───
        public decimal NetSalary { get; set; }
        public string AutoNote { get; set; } = "";

        // ─── بيانات مساعدة لعرض فواتير الشهر بالشاشة (لا تدخل في حساب الراتب مباشرة) ───
        public decimal TotalSalesAmount { get; set; }
        public decimal CashTotal { get; set; }
        public decimal KnetTotal { get; set; }
        public decimal CustomerDebtTotal { get; set; }
        public decimal SalesTarget { get; set; }
        public bool TargetReached { get; set; }
        public decimal NormalCommissionRate { get; set; }
        public decimal CommissionAfterTargetRate { get; set; }
        public List<Sale> ActiveSales { get; set; } = new();
        public List<Sale> CancelledSales { get; set; } = new();
    }

    /// <summary>
    /// معادلة موحّدة لتسوية راتب الموظف الشهرية: الاستحقاقات (أساسي + عمولة + بدلات + خدمات
    /// وإكراميات الفواتير)، دين الموظف من الفواتير، ورصيد السلف (مرحّل من الأشهر السابقة + جديد
    /// هذا الشهر)، وصولاً لتحديد المبلغ الذي يُخصم فعلياً من السلف والمتبقي المرحّل للشهر القادم،
    /// وصافي الراتب المستحق للصرف. كل الأرقام المرتبطة بمصادر أخرى (سلف/فواتير) تُحسب هنا من
    /// سجلاتها الأصلية مباشرة، ولا تُدخَل يدوياً.
    /// </summary>
    public static class SalarySettlementCalculator
    {
        public static async Task<SalarySettlementResult> ComputeAsync(
            ApplicationDbContext context, Employee employee, int month, int year,
            decimal basicSalary, decimal allowances, decimal deductions, string monthLabel)
        {
            var rangeStart = new DateTime(year, month, 1);
            var rangeEnd = rangeStart.AddMonths(1);

            var allSales = await context.Sales
                .Where(s => s.EmployeeId == employee.Id && s.SaleDate >= rangeStart && s.SaleDate < rangeEnd)
                .OrderBy(s => s.SaleDate)
                .ToListAsync();

            var activeSales = allSales.Where(s => s.Status != "ملغي").ToList();
            var cancelledSales = allSales.Where(s => s.Status == "ملغي").ToList();

            string[] cashMethods = { "كاش", "نقدي", "Cash" };
            string[] knetMethods = { "كي نت", "بطاقة", "تحويل بنكي", "K-Net" };
            string[] mixedMethods = { "كي نت و كاش", "مناصفة", "Cash & K-Net" };

            decimal totalSalesAmount = activeSales.Sum(s => s.NetAmount);
            bool targetReached = employee.SalesTarget.HasValue
                                 && employee.SalesTarget.Value > 0
                                 && totalSalesAmount >= employee.SalesTarget.Value;
            decimal effectiveCommissionRate = (targetReached && employee.CommissionAfterTarget.HasValue)
                                      ? employee.CommissionAfterTarget.Value
                                      : employee.Commission;
            decimal commissionAmount = Math.Round(totalSalesAmount * effectiveCommissionRate / 100, 3);

            decimal cashTotal = Math.Round(activeSales.Sum(s =>
                cashMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethods.Contains(s.PaymentMethod) ? (s.CashAmount ?? 0) : 0), 3);
            decimal knetTotal = Math.Round(activeSales.Sum(s =>
                knetMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethods.Contains(s.PaymentMethod) ? (s.LinkAmount ?? 0) : 0), 3);
            decimal employeeDebtTotal = Math.Round(activeSales.Where(s => s.PaymentMethod == "دين على الموظف").Sum(s => s.NetAmount), 3);
            decimal customerDebtTotal = Math.Round(activeSales.Where(s => s.PaymentMethod == "دين على العميل").Sum(s => s.NetAmount), 3);

            decimal totalGifts = activeSales.Where(s => s.EmployeeGift.HasValue && s.EmployeeGift > 0).Sum(s => s.EmployeeGift!.Value);
            decimal totalHadiya = activeSales.Where(s => s.GiftForEmployee.HasValue && s.GiftForEmployee > 0).Sum(s => s.GiftForEmployee!.Value);

            // رصيد السلف القائم (غير مسدد بالكامل بعد) — نفصله حسب تاريخ السلفة: قبل بداية الشهر
            // الحالي (مرحّل) أو خلاله (جديد)، حتى نعرف أصل كل جزء من رصيد السلف المستحق.
            var outstandingAdvances = await context.EmployeeAdvances
                .Where(a => a.EmployeeId == employee.Id
                         && (a.Status == EmployeeAdvance.Statuses.Disbursed || a.Status == EmployeeAdvance.Statuses.Transferred)
                         && a.PaidDate == null)
                .ToListAsync();

            decimal carriedAdvanceBalance = Math.Round(outstandingAdvances
                .Where(a => a.AdvanceDate < rangeStart)
                .Sum(a => a.Amount - a.DeductedAmount), 3);
            decimal newAdvancesAmount = Math.Round(outstandingAdvances
                .Where(a => a.AdvanceDate >= rangeStart && a.AdvanceDate < rangeEnd)
                .Sum(a => a.Amount - a.DeductedAmount), 3);
            decimal totalAdvanceDue = carriedAdvanceBalance + newAdvancesAmount;

            decimal totalEntitlements = basicSalary + commissionAmount + allowances + totalGifts + totalHadiya;
            decimal availableForAdvanceRepayment = totalEntitlements - deductions - employeeDebtTotal;

            decimal advanceDeducted = Math.Max(0, Math.Min(totalAdvanceDue, availableForAdvanceRepayment));
            decimal remainingAdvanceCarried = totalAdvanceDue - advanceDeducted;
            decimal netSalary = availableForAdvanceRepayment - advanceDeducted;

            string autoNote = BuildAutoNote(monthLabel, year, totalEntitlements, deductions, employeeDebtTotal,
                advanceDeducted, remainingAdvanceCarried, netSalary);

            return new SalarySettlementResult
            {
                BasicSalary = basicSalary,
                CommissionAmount = commissionAmount,
                Allowances = allowances,
                GiftAmount = totalGifts,
                HadiyaAmount = totalHadiya,
                TotalEntitlements = totalEntitlements,

                Deductions = deductions,
                EmployeeDebtTotal = employeeDebtTotal,
                CarriedAdvanceBalance = carriedAdvanceBalance,
                NewAdvancesAmount = newAdvancesAmount,
                TotalAdvanceDue = totalAdvanceDue,
                AvailableForAdvanceRepayment = availableForAdvanceRepayment,
                AdvanceDeducted = advanceDeducted,
                RemainingAdvanceCarried = remainingAdvanceCarried,

                NetSalary = netSalary,
                AutoNote = autoNote,

                TotalSalesAmount = totalSalesAmount,
                CashTotal = cashTotal,
                KnetTotal = knetTotal,
                CustomerDebtTotal = customerDebtTotal,
                SalesTarget = employee.SalesTarget ?? 0,
                TargetReached = targetReached,
                NormalCommissionRate = employee.Commission,
                CommissionAfterTargetRate = employee.CommissionAfterTarget ?? 0,
                ActiveSales = activeSales,
                CancelledSales = cancelledSales
            };
        }

        private static string BuildAutoNote(string monthLabel, int year, decimal totalEntitlements, decimal deductions,
            decimal employeeDebtTotal, decimal advanceDeducted, decimal remainingAdvanceCarried, decimal netSalary)
        {
            var clauses = new List<string>();
            if (deductions > 0)
                clauses.Add($"خُصم {deductions:N3} د.ك خصومات");
            if (employeeDebtTotal > 0)
                clauses.Add($"خُصم دين على الموظف {employeeDebtTotal:N3} د.ك");
            if (advanceDeducted > 0)
                clauses.Add($"تم خصم {advanceDeducted:N3} د.ك من رصيد السلف");
            clauses.Add(remainingAdvanceCarried > 0
                ? $"ترحيل {remainingAdvanceCarried:N3} د.ك للشهر التالي"
                : "لا يوجد رصيد سلف مرحّل للشهر التالي");

            string note = $"تمت تسوية راتب {monthLabel} {year}. إجمالي الاستحقاقات {totalEntitlements:N3} د.ك";
            note += "، " + string.Join("، و", clauses);
            note += $". صافي المبلغ المصروف {netSalary:N3} د.ك.";
            return note;
        }
    }
}
