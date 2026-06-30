using Microsoft.EntityFrameworkCore;
using Salon.Data;

namespace Salon.Services
{
    public class ReminderWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReminderWorker> _logger;

        public ReminderWorker(IServiceScopeFactory scopeFactory, ILogger<ReminderWorker> logger)
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
                    await ProcessDueRemindersAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ReminderWorker");
                }
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ProcessDueRemindersAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var now = DateTime.Now;

            var dueReminders = await context.AppointmentReminders
                .Include(r => r.Appointment)
                    .ThenInclude(a => a!.Customer)
                .Include(r => r.Appointment)
                    .ThenInclude(a => a!.Employee)
                .Where(r => !r.IsSent && r.Appointment != null
                         && r.Appointment.AppointmentDate.AddMinutes(-r.MinutesBefore) <= now
                         && r.Appointment.Status == "مجدول")
                .ToListAsync();

            foreach (var reminder in dueReminders)
            {
                var appt = reminder.Appointment!;
                var customerName = appt.Customer?.FullName ?? "غير محدد";
                var employeeName = appt.Employee?.FullName;

                await emailService.SendAppointmentReminderAsync(customerName, employeeName, appt.AppointmentDate, reminder.MinutesBefore);

                reminder.IsSent = true;
                reminder.SentAt = now;
                _logger.LogInformation("Sent reminder for appointment {Id} ({MinutesBefore} min before)", appt.Id, reminder.MinutesBefore);
            }

            if (dueReminders.Count > 0)
                await context.SaveChangesAsync();
        }
    }
}
