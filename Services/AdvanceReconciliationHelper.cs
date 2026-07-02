using Microsoft.EntityFrameworkCore;
using Salon.Data;

namespace Salon.Services
{
    public static class AdvanceReconciliationHelper
    {
        public static async Task ReconcileAsync(ApplicationDbContext context, int employeeId, decimal amountToDeduct)
        {
            if (amountToDeduct <= 0) return;

            var advances = await context.EmployeeAdvances
                .Where(a => a.EmployeeId == employeeId && a.Status == "موافق عليها" && a.PaidDate == null)
                .OrderBy(a => a.AdvanceDate)
                .ToListAsync();

            decimal remaining = amountToDeduct;
            foreach (var advance in advances)
            {
                if (remaining <= 0) break;

                decimal advanceRemaining = advance.Amount - advance.DeductedAmount;
                if (advanceRemaining <= remaining)
                {
                    remaining -= advanceRemaining;
                    advance.DeductedAmount = advance.Amount;
                    advance.PaidDate = DateTime.Today;
                    advance.Status = "مسددة";
                }
                else
                {
                    advance.DeductedAmount += remaining;
                    remaining = 0;
                }
            }
        }

        // يعكس أثر ReconcileAsync عند حذف راتب كان قد خصم من سلفة الموظف - يبدأ بأحدث سلفة
        // اتخصم منها (حسب تاريخ السداد) باعتبارها الأرجح أنها تأثرت بالراتب المحذوف
        public static async Task UnreconcileAsync(ApplicationDbContext context, int employeeId, decimal amountToReverse)
        {
            if (amountToReverse <= 0) return;

            var advances = await context.EmployeeAdvances
                .Where(a => a.EmployeeId == employeeId && a.DeductedAmount > 0)
                .OrderByDescending(a => a.PaidDate ?? a.AdvanceDate)
                .ToListAsync();

            decimal remaining = amountToReverse;
            foreach (var advance in advances)
            {
                if (remaining <= 0) break;

                decimal reduceBy = Math.Min(advance.DeductedAmount, remaining);
                advance.DeductedAmount -= reduceBy;
                remaining -= reduceBy;

                if (advance.DeductedAmount < advance.Amount)
                {
                    advance.Status = "موافق عليها";
                    advance.PaidDate = null;
                }
            }
        }
    }
}
