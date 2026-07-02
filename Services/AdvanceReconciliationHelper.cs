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
    }
}
