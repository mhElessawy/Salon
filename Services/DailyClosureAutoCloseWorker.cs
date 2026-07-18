using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Services
{
    /// <summary>
    /// يفحص كل دقيقة (نفس نمط ReminderWorker) هل تجاوزنا وقت الإغلاق التلقائي المضبوط من
    /// الإعدادات (AppSettings["DailyClosureCutoffTime"], افتراضي 23:59) بدون اعتماد الكاشير
    /// لليومية — لو نعم يقفلها آلياً بحالة "بانتظار الاعتماد" حتى يراجعها لاحقاً.
    /// </summary>
    public class DailyClosureAutoCloseWorker : BackgroundService
    {
        public const string CutoffSettingKey = "DailyClosureCutoffTime";
        public const string DefaultCutoff = "23:59";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DailyClosureAutoCloseWorker> _logger;

        public DailyClosureAutoCloseWorker(IServiceScopeFactory scopeFactory, ILogger<DailyClosureAutoCloseWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAutoCloseAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in DailyClosureAutoCloseWorker");
                }
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ProcessAutoCloseAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

            var now = DateTime.Now;
            var cutoffValue = await context.AppSettings.FindAsync(CutoffSettingKey);
            var cutoff = TimeSpan.TryParse(cutoffValue?.Value, out var parsed) ? parsed : TimeSpan.Parse(DefaultCutoff);

            if (now.TimeOfDay < cutoff) return;

            var today = now.Date;
            var shift = await context.Shifts
                .Where(s => s.ShiftDate.Date == today)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (shift == null || shift.ApprovalStatus != Shift.ApprovalStatuses.Open) return;

            shift.ApprovalStatus = Shift.ApprovalStatuses.AutoClosedUnapproved;
            shift.IsAutoClosed = true;
            shift.AutoClosedAt = now;
            shift.Status = "مغلق";
            await context.SaveChangesAsync();

            await audit.LogAsync("AutoClose", "Shifts",
                $"إغلاق آلي لليومية بتاريخ {shift.ShiftDate:yyyy/MM/dd} — لم يتم اعتمادها من الكاشير", shift.Id);

            _logger.LogInformation("Auto-closed unapproved daily closure for {Date}", shift.ShiftDate);
        }
    }
}
