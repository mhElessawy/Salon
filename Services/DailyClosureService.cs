using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Services
{
    public interface IDailyClosureService
    {
        Task<Shift?> FindForDateAsync(DateTime date);
        Task<Shift> GetOrCreateForDateAsync(DateTime date);
        Task<bool> IsDateLockedAsync(DateTime date);
    }

    /// <summary>
    /// منطق "اليومية" (سجل Shift الموسَّع) المشترك بين شاشة الاعتماد وكل الكنترولرز التي
    /// تحتاج تمنع التعديل على بيانات يوم معتمد — نقطة واحدة لتعريف "هل هذا التاريخ مقفول؟".
    /// </summary>
    public class DailyClosureService : IDailyClosureService
    {
        private readonly ApplicationDbContext _context;

        public DailyClosureService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Shift?> FindForDateAsync(DateTime date)
        {
            var day = date.Date;
            return await _context.Shifts
                .Where(s => s.ShiftDate.Date == day)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<Shift> GetOrCreateForDateAsync(DateTime date)
        {
            var day = date.Date;
            var existing = await FindForDateAsync(day);
            if (existing != null) return existing;

            var shift = new Shift
            {
                Name = $"يومية {day:yyyy/MM/dd}",
                ShiftDate = day,
                StartTime = TimeSpan.Zero,
                OpeningBalance = 0,
                CreatedAt = DateTime.Now,
                ApprovalStatus = Shift.ApprovalStatuses.Open
            };
            _context.Shifts.Add(shift);
            await _context.SaveChangesAsync();
            return shift;
        }

        public async Task<bool> IsDateLockedAsync(DateTime date)
        {
            var day = date.Date;
            var shift = await FindForDateAsync(day);
            if (shift == null) return false;

            if (shift.ApprovalStatus == Shift.ApprovalStatuses.Approved
                || shift.ApprovalStatus == Shift.ApprovalStatuses.ApprovedWithDiscrepancy)
                return true;

            // يوم سابق أُغلق آلياً بلا اعتماد يبقى محمياً من التعديل حتى تتم مراجعته، أما اليوم
            // الحالي (لسه شغال) فلا يُقفل بمجرد تجاوز وقت الإغلاق الآلي.
            if (shift.ApprovalStatus == Shift.ApprovalStatuses.AutoClosedUnapproved && day < DateTime.Today)
                return true;

            return false;
        }
    }
}
