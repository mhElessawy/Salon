using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Services
{
    /// <summary>
    /// رصيد عهدة الموظف موحَّد: أي طلب شراء أو دفعة تُخصم من مجموع كل إيداعات الموظف المفتوحة
    /// (غير المسوَّاة) معاً، مش من إيداع واحد بعينه — حتى لو موزَّع رصيده على أكثر من إيداع.
    /// كل الشاشات التي تتحقق من رصيد عهدة موظف أو تعرضه لازم تستخدم هذا الحساب بدل قراءة
    /// Custody.RemainingAmount لصف واحد لوحده.
    /// </summary>
    public static class CustodyPoolCalculator
    {
        public static IQueryable<Custody> OpenCustodiesQuery(ApplicationDbContext context, int employeeId) =>
            context.Custodies
                .Include(c => c.Allocations)
                .Include(c => c.InvoicePayments)
                .Where(c => c.EmployeeId == employeeId && c.SettlementType == null);

        public static Task<List<Custody>> GetOpenCustodiesAsync(ApplicationDbContext context, int employeeId) =>
            OpenCustodiesQuery(context, employeeId).ToListAsync();

        public static Task<decimal> GetReservedAsync(ApplicationDbContext context, int employeeId) =>
            context.PurchaseRequests
                .Where(p => p.EmployeeId == employeeId
                         && (p.Status == PurchaseRequest.Statuses.Pending || p.Status == PurchaseRequest.Statuses.Approved))
                .SumAsync(p => p.EstimatedAmount);

        public static decimal PoolDeposited(this IEnumerable<Custody> openCustodies) => openCustodies.Sum(c => c.Amount);

        public static decimal PoolSpent(this IEnumerable<Custody> openCustodies) => openCustodies.Sum(c => c.SpentAmount);

        public static decimal PoolRemaining(this IEnumerable<Custody> openCustodies) => openCustodies.Sum(c => c.RemainingAmount);

        /// <summary>رصيد الموظف الموحَّد الكامل (إيداعات مفتوحة + مصروف + محجوز + متاح لطلب جديد).</summary>
        public static async Task<EmployeeCustodyPool> GetPoolAsync(ApplicationDbContext context, int employeeId)
        {
            var open = await GetOpenCustodiesAsync(context, employeeId);
            var reserved = await GetReservedAsync(context, employeeId);
            return new EmployeeCustodyPool(employeeId, open.PoolDeposited(), open.PoolSpent(), reserved);
        }
    }

    public record EmployeeCustodyPool(int EmployeeId, decimal TotalDeposited, decimal TotalSpent, decimal TotalReserved)
    {
        public decimal RemainingAmount => TotalDeposited - TotalSpent;
        public decimal AvailableForRequest => RemainingAmount - TotalReserved;
    }
}