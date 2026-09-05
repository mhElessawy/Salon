using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Services
{
    public interface IDailyClosureService
    {
        Task<Shift?> FindForDateAsync(DateTime date, string department);
        Task<Shift> GetOrCreateForDateAsync(DateTime date, string department);
        Task<bool> IsDateLockedAsync(DateTime date, string? rawDepartment);

        /// <summary>
        /// أحدث (قسم، تاريخ) خلال آخر 90 يوم فيه مبيعات فعلية ومفيهوش سجل اعتماد معتمد بعد —
        /// نقطة واحدة يستخدمها التنبيه المنبثق ولوحة التحكم وشاشة الاعتماد نفسها حتى ما يفترقوش.
        /// </summary>
        Task<(string Department, DateTime Date)?> FindMostRecentPendingApprovalAsync(
            IEnumerable<string> departments, DateTime? excludeDate = null, string? excludeDepartment = null);

        /// <summary>كل (قسم، تاريخ) بانتظار الاعتماد خلال آخر 90 يوم، مرتبة تصاعدياً بالتاريخ.</summary>
        Task<List<(string Department, DateTime Date)>> FindAllPendingApprovalsAsync(IEnumerable<string> departments);
    }

    /// <summary>
    /// منطق "اليومية" (سجل Shift الموسَّع) المشترك بين شاشة الاعتماد وكل الكنترولرز التي
    /// تحتاج تمنع التعديل على بيانات يوم معتمد — نقطة واحدة لتعريف "هل هذا التاريخ/القسم مقفول؟".
    /// كل قسم (حلاقة/مساج/عام) له يومية مستقلة تماماً عن باقي الأقسام لنفس التاريخ.
    /// </summary>
    public class DailyClosureService : IDailyClosureService
    {
        private readonly ApplicationDbContext _context;

        public DailyClosureService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Shift?> FindForDateAsync(DateTime date, string department)
        {
            var day = date.Date;
            return await _context.Shifts
                .Where(s => s.ShiftDate.Date == day && s.IsClosureRecord && s.ClosureDepartment == department)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<Shift> GetOrCreateForDateAsync(DateTime date, string department)
        {
            var day = date.Date;
            var existing = await FindForDateAsync(day, department);
            if (existing != null) return existing;

            var shift = new Shift
            {
                Name = $"يومية {department} {day:yyyy/MM/dd}",
                ShiftDate = day,
                StartTime = TimeSpan.Zero,
                OpeningBalance = 0,
                CreatedAt = DateTime.Now,
                ApprovalStatus = Shift.ApprovalStatuses.Open,
                IsClosureRecord = true,
                ClosureDepartment = department
            };
            _context.Shifts.Add(shift);
            await _context.SaveChangesAsync();
            return shift;
        }

        // rawDepartment بياخد أي قيمة قسم خام (مثل SaleType أو Expense.Department) وتترجم تلقائياً
        // لنطاق الإغلاق الصحيح (حلاقة/مساج/عام) قبل الفحص.
        public async Task<bool> IsDateLockedAsync(DateTime date, string? rawDepartment)
        {
            var day = date.Date;
            var department = Shift.ClosureDepartments.Normalize(rawDepartment);
            var shift = await FindForDateAsync(day, department);
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

        public async Task<(string Department, DateTime Date)?> FindMostRecentPendingApprovalAsync(
            IEnumerable<string> departments, DateTime? excludeDate = null, string? excludeDepartment = null)
        {
            var all = await FindAllPendingApprovalsAsync(departments);
            var filtered = all.Where(p => !(p.Department == excludeDepartment && p.Date == excludeDate?.Date));
            return filtered.Any() ? filtered.OrderByDescending(p => p.Date).First() : null;
        }

        public async Task<List<(string Department, DateTime Date)>> FindAllPendingApprovalsAsync(IEnumerable<string> departments)
        {
            var deptList = departments.Distinct().ToList();
            if (deptList.Count == 0) return new List<(string, DateTime)>();

            var lookbackStart = DateTime.Today.AddDays(-90);

            var approvedRows = await _context.Shifts
                .Where(s => s.IsClosureRecord && s.ClosureDepartment != null && deptList.Contains(s.ClosureDepartment!)
                    && s.ShiftDate >= lookbackStart && s.ShiftDate < DateTime.Today
                    && (s.ApprovalStatus == Shift.ApprovalStatuses.Approved
                        || s.ApprovalStatus == Shift.ApprovalStatuses.ApprovedWithDiscrepancy))
                .Select(s => new { Department = s.ClosureDepartment!, Date = s.ShiftDate.Date })
                .ToListAsync();
            var approvedSet = new HashSet<(string, DateTime)>(approvedRows.Select(r => (r.Department, r.Date)));

            var salesRows = await _context.Sales
                .Where(s => s.SaleDate >= lookbackStart && s.SaleDate < DateTime.Today && s.Status != "ملغي")
                .Select(s => new { s.SaleType, s.Department, Date = s.SaleDate.Date })
                .Distinct()
                .ToListAsync();

            var pending = new List<(string Department, DateTime Date)>();
            foreach (var dept in deptList)
            {
                bool isShared = dept == Shift.ClosureDepartments.Shared;
                var deptSaleDates = salesRows
                    .Where(r => isShared
                        ? (r.SaleType != Shift.ClosureDepartments.Haircut && r.SaleType != Shift.ClosureDepartments.Massage
                            && !(r.SaleType == "منتجات" && (r.Department == Shift.ClosureDepartments.Haircut || r.Department == Shift.ClosureDepartments.Massage)))
                        : (r.SaleType == dept || (r.SaleType == "منتجات" && r.Department == dept)))
                    .Select(r => r.Date)
                    .Distinct();

                foreach (var d in deptSaleDates)
                {
                    if (!approvedSet.Contains((dept, d))) pending.Add((dept, d));
                }
            }
            return pending.OrderBy(p => p.Date).ToList();
        }
    }
}