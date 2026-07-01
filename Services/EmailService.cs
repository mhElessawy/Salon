using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Salon.Models;
using System.Net.Mail;

namespace Salon.Services;

public class EmailSettings
{
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SenderEmail { get; set; } = "";
    public string SenderPassword { get; set; } = "";
    public string SenderName { get; set; } = "";
    public string NotificationEmail { get; set; } = "";
}

public interface IEmailService
{
    Task SendInvoiceNotificationAsync(Sale sale, string cashierName);
    Task SendAttendanceNotificationAsync(string employeeName, string department, string action, TimeSpan actionTime, DateTime date, string? extra = null);
    Task SendInvoiceCancellationAsync(Sale sale, string cancelledBy);
    Task SendAdvanceRequestAsync(string employeeName, string department, decimal amount, string? reason, DateTime requestDate);
    Task SendAppointmentReminderAsync(string customerName, string? employeeName, DateTime appointmentDate, int minutesBefore);
}

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(EmailSettings settings, ILogger<EmailService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SendInvoiceNotificationAsync(Sale sale, string cashierName)
    {
        if (string.IsNullOrWhiteSpace(_settings.SenderEmail) ||
            _settings.SenderEmail.StartsWith("YOUR_"))
        {
            _logger.LogWarning("Email not configured — skipping invoice notification.");
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(_settings.NotificationEmail));
            message.Subject = $"عملية بيع جديدة — فاتورة {sale.InvoiceNumber}";
            message.Body = new TextPart("html") { Text = BuildEmailBody(sale, cashierName) };

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_settings.SenderEmail, _settings.SenderPassword);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invoice email for {InvoiceNumber}", sale.InvoiceNumber);
        }
    }

    public async Task SendAttendanceNotificationAsync(
        string employeeName, string department, string action,
        TimeSpan actionTime, DateTime date, string? extra = null)
    {
        if (string.IsNullOrWhiteSpace(_settings.SenderEmail) ||
            _settings.SenderEmail.StartsWith("YOUR_"))
        {
            _logger.LogWarning("Email not configured — skipping attendance notification.");
            return;
        }
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(_settings.NotificationEmail));
            message.Subject = $"{action} — {employeeName}  |  {date:dd/MM/yyyy}";
            message.Body = new TextPart("html")
            {
                Text = BuildAttendanceEmailBody(employeeName, department, action, actionTime, date, extra)
            };

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_settings.SenderEmail, _settings.SenderPassword);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send attendance email for {Employee} — {Action}", employeeName, action);
        }
    }

    private static string BuildAttendanceEmailBody(
        string employeeName, string department, string action,
        TimeSpan actionTime, DateTime date, string? extra)
    {
        // لون ورمز حسب نوع الإجراء
        var (headerColor, icon, actionAr) = action switch
        {
            "حضور" => ("#1a7a4a", "✅", "تسجيل حضور"),
            "استئذان" => ("#e07b00", "🚪", "خروج باستئذان"),
            "عودة" => ("#0d6efd", "↩️", "عودة من الاستئذان"),
            "انصراف" => ("#c0392b", "🔴", "تسجيل انصراف"),
            _ => ("#555", "📋", action)
        };

        var timeStr = $"{actionTime.Hours:D2}:{actionTime.Minutes:D2}";
        var dateStr = date.ToString("dddd dd/MM/yyyy", new System.Globalization.CultureInfo("ar-EG"));
        var extraRow = string.IsNullOrEmpty(extra) ? "" : $@"
        <tr>
          <td style='padding:5px 0;font-size:14px;color:#555;'>ملاحظة:</td>
          <td style='padding:5px 0;font-size:14px;color:#1a1a2e;font-weight:600;text-align:left;'>{extra}</td>
        </tr>";

        return $@"<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head><meta charset='utf-8'/></head>
<body style='margin:0;padding:0;background:#f0f2f5;font-family:Segoe UI,Tahoma,Arial,sans-serif;direction:rtl;'>
<table width='100%' cellpadding='0' cellspacing='0' style='background:#f0f2f5;padding:30px 0;'>
<tr><td align='center'>
<table width='520' cellpadding='0' cellspacing='0'
       style='background:#fff;border-radius:14px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.10);'>

  <!-- Header -->
  <tr>
    <td style='background:linear-gradient(135deg,#1a1a2e,#0f3460);padding:22px 28px;'>
      <h2 style='margin:0;color:#F7941D;font-size:20px;'>{icon} {actionAr}</h2>
      <p style='margin:4px 0 0;color:rgba(255,255,255,0.65);font-size:13px;'>معهد موس للرجال</p>
    </td>
  </tr>

  <!-- Action Badge -->
  <tr>
    <td style='padding:18px 28px 8px;'>
      <div style='display:inline-block;background:{headerColor};color:#fff;
                  border-radius:8px;padding:8px 18px;font-size:16px;font-weight:700;'>
        {actionAr}
      </div>
    </td>
  </tr>

  <!-- Details -->
  <tr>
    <td style='padding:10px 28px 20px;'>
      <table width='100%' cellpadding='0' cellspacing='0'
             style='background:#f7f8fa;border-radius:10px;padding:16px 18px;'>
        <tr>
          <td style='padding:6px 0;font-size:14px;color:#555;'>الموظف:</td>
          <td style='padding:6px 0;font-size:15px;font-weight:700;color:#1a1a2e;text-align:left;'>{employeeName}</td>
        </tr>
        <tr>
          <td style='padding:6px 0;font-size:14px;color:#555;'>القسم:</td>
          <td style='padding:6px 0;font-size:14px;color:#1a1a2e;text-align:left;'>{department}</td>
        </tr>
        <tr>
          <td style='padding:6px 0;font-size:14px;color:#555;'>التاريخ:</td>
          <td style='padding:6px 0;font-size:14px;color:#1a1a2e;text-align:left;'>{dateStr}</td>
        </tr>
        <tr>
          <td style='padding:6px 0;font-size:15px;font-weight:700;color:#555;'>الوقت:</td>
          <td style='padding:6px 0;font-size:20px;font-weight:800;color:{headerColor};
                     letter-spacing:2px;text-align:left;'>{timeStr}</td>
        </tr>
        {extraRow}
      </table>
    </td>
  </tr>

  <!-- Footer -->
  <tr>
    <td style='background:#f7f8fa;padding:12px 28px;border-top:1px solid #eee;
               font-size:12px;color:#aaa;text-align:center;'>
      نظام معهد موس &nbsp;|&nbsp; {DateTime.Now:HH:mm  dd/MM/yyyy}
    </td>
  </tr>

</table>
</td></tr>
</table>
</body>
</html>";
    }

    public async Task SendInvoiceCancellationAsync(Sale sale, string cancelledBy)
    {
        if (string.IsNullOrWhiteSpace(_settings.SenderEmail) ||
            _settings.SenderEmail.StartsWith("YOUR_"))
        {
            _logger.LogWarning("Email not configured — skipping cancellation notification.");
            return;
        }
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(_settings.NotificationEmail));
            message.Subject = $"❌ إلغاء فاتورة — {sale.InvoiceNumber}";
            message.Body = new TextPart("html") { Text = BuildCancellationEmailBody(sale, cancelledBy) };

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_settings.SenderEmail, _settings.SenderPassword);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send cancellation email for {InvoiceNumber}", sale.InvoiceNumber);
        }
    }

    private static string BuildCancellationEmailBody(Sale sale, string cancelledBy)
    {
        var itemsHtml = new System.Text.StringBuilder();
        var employeeName = sale.Employee?.FullName ?? "—";
        var customerName = sale.Customer?.FullName ?? "زائر";

        foreach (var item in sale.SaleItems)
        {
            itemsHtml.Append($@"
            <div style='padding:8px 0;border-bottom:1px solid #fce4e4;'>
                <div style='font-weight:600;font-size:14px;color:#1a1a2e;'>{item.ItemName}</div>
                <div style='font-size:12px;color:#888;margin-top:2px;'>{item.Quantity} × {item.Price:N3} د.ك</div>
            </div>");
        }

        var dateStr = sale.SaleDate.ToString("MMM yyyy, HH:mm dd");
        var nowStr = DateTime.Now.ToString("HH:mm  dd/MM/yyyy");

        return $@"<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head><meta charset='utf-8'/></head>
<body style='margin:0;padding:0;background:#f0f2f5;font-family:Segoe UI,Tahoma,Arial,sans-serif;direction:rtl;'>
<table width='100%' cellpadding='0' cellspacing='0' style='background:#f0f2f5;padding:30px 0;'>
<tr><td align='center'>
<table width='540' cellpadding='0' cellspacing='0'
       style='background:#fff;border-radius:14px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.12);'>

  <tr>
    <td style='background:linear-gradient(135deg,#c0392b,#8e1a10);padding:24px 28px;'>
      <h2 style='margin:0;color:#fff;font-size:22px;'>❌ تم إلغاء فاتورة</h2>
      <p style='margin:5px 0 0;color:rgba(255,255,255,0.7);font-size:13px;'>معهد وصالون موس للرجال</p>
    </td>
  </tr>

  <tr>
    <td style='padding:20px 28px 12px;'>
      <table width='100%' cellpadding='0' cellspacing='0'>
        <tr>
          <td style='padding:5px 0;font-size:14px;color:#555;'>رقم الفاتورة:</td>
          <td style='padding:5px 0;font-size:15px;font-weight:700;color:#c0392b;text-align:left;'>{sale.InvoiceNumber}</td>
        </tr>
        <tr>
          <td style='padding:5px 0;font-size:14px;color:#555;'>القسم:</td>
          <td style='padding:5px 0;font-size:14px;color:#1a1a2e;text-align:left;'>{sale.SaleType}</td>
        </tr>
        <tr>
          <td style='padding:5px 0;font-size:14px;color:#555;'>تاريخ الفاتورة:</td>
          <td style='padding:5px 0;font-size:14px;color:#555;text-align:left;'>{dateStr}</td>
        </tr>
        <tr>
          <td style='padding:5px 0;font-size:14px;color:#555;'>العميل:</td>
          <td style='padding:5px 0;font-size:14px;color:#1a1a2e;text-align:left;'>{customerName}</td>
        </tr>
        <tr>
          <td style='padding:5px 0;font-size:14px;color:#555;'>الموظف:</td>
          <td style='padding:5px 0;font-size:14px;color:#1a1a2e;text-align:left;'>{employeeName}</td>
        </tr>
        <tr>
          <td style='padding:5px 0;font-size:14px;color:#c0392b;font-weight:700;'>ألغيت بواسطة:</td>
          <td style='padding:5px 0;font-size:14px;color:#c0392b;font-weight:700;text-align:left;'>{cancelledBy}</td>
        </tr>
        <tr>
          <td style='padding:5px 0;font-size:14px;color:#c0392b;font-weight:700;'>وقت الإلغاء:</td>
          <td style='padding:5px 0;font-size:14px;color:#c0392b;font-weight:700;text-align:left;'>{nowStr}</td>
        </tr>
      </table>
    </td>
  </tr>

  <tr><td style='padding:0 28px;'><hr style='border:none;border-top:2px dashed #fce4e4;margin:0;'/></td></tr>

  <tr>
    <td style='padding:14px 28px 8px;'>
      <p style='margin:0 0 8px;font-weight:700;font-size:14px;color:#555;'>بنود الفاتورة الملغاة:</p>
      {itemsHtml}
    </td>
  </tr>

  <tr>
    <td style='padding:12px 28px 20px;'>
      <table width='100%' cellpadding='0' cellspacing='0'
             style='background:#fff5f5;border-radius:10px;padding:14px 18px;border:1px solid #fce4e4;'>
        <tr>
          <td style='font-size:16px;font-weight:700;color:#c0392b;'>إجمالي الفاتورة الملغاة:</td>
          <td style='font-size:18px;font-weight:800;color:#c0392b;text-align:left;'>{sale.NetAmount:N3} د.ك</td>
        </tr>
      </table>
    </td>
  </tr>

  <tr>
    <td style='background:#fff5f5;padding:12px 28px;border-top:1px solid #fce4e4;
               font-size:12px;color:#aaa;text-align:center;'>
      نظام معهد موس &nbsp;|&nbsp; {nowStr}
    </td>
  </tr>

</table>
</td></tr>
</table>
</body>
</html>";
    }

    public async Task SendAdvanceRequestAsync(string employeeName, string department, decimal amount, string? reason, DateTime requestDate)
    {
        if (string.IsNullOrWhiteSpace(_settings.SenderEmail) || _settings.SenderEmail.StartsWith("YOUR_"))
        {
            _logger.LogWarning("Email not configured — skipping advance request notification.");
            return;
        }
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(_settings.NotificationEmail));
            message.Subject = $"💰 طلب سلفة جديد — {employeeName}";
            message.Body = new TextPart("html") { Text = BuildAdvanceRequestEmailBody(employeeName, department, amount, reason, requestDate) };

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_settings.SenderEmail, _settings.SenderPassword);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send advance request email for {Employee}", employeeName);
        }
    }

    private static string BuildAdvanceRequestEmailBody(string employeeName, string department, decimal amount, string? reason, DateTime requestDate)
    {
        var dateStr = requestDate.ToString("dddd dd/MM/yyyy", new System.Globalization.CultureInfo("ar-EG"));
        var nowStr = DateTime.Now.ToString("HH:mm  dd/MM/yyyy");
        var reasonRow = string.IsNullOrEmpty(reason) ? "" : $@"
        <tr>
          <td style='padding:6px 0;font-size:14px;color:#555;'>السبب:</td>
          <td style='padding:6px 0;font-size:14px;color:#1a1a2e;font-weight:600;text-align:left;'>{reason}</td>
        </tr>";

        return $@"<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head><meta charset='utf-8'/></head>
<body style='margin:0;padding:0;background:#f0f2f5;font-family:Segoe UI,Tahoma,Arial,sans-serif;direction:rtl;'>
<table width='100%' cellpadding='0' cellspacing='0' style='background:#f0f2f5;padding:30px 0;'>
<tr><td align='center'>
<table width='520' cellpadding='0' cellspacing='0'
       style='background:#fff;border-radius:14px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.10);'>

  <tr>
    <td style='background:linear-gradient(135deg,#1a1a2e,#0f3460);padding:22px 28px;'>
      <h2 style='margin:0;color:#F7941D;font-size:20px;'>💰 طلب سلفة جديد</h2>
      <p style='margin:4px 0 0;color:rgba(255,255,255,0.65);font-size:13px;'>معهد موس للرجال — ينتظر موافقتك</p>
    </td>
  </tr>

  <tr>
    <td style='padding:18px 28px 8px;'>
      <div style='display:inline-block;background:#F7941D;color:#fff;
                  border-radius:8px;padding:8px 18px;font-size:16px;font-weight:700;'>
        ينتظر الموافقة
      </div>
    </td>
  </tr>

  <tr>
    <td style='padding:10px 28px 20px;'>
      <table width='100%' cellpadding='0' cellspacing='0'
             style='background:#f7f8fa;border-radius:10px;padding:16px 18px;'>
        <tr>
          <td style='padding:6px 0;font-size:14px;color:#555;'>الموظف:</td>
          <td style='padding:6px 0;font-size:15px;font-weight:700;color:#1a1a2e;text-align:left;'>{employeeName}</td>
        </tr>
        <tr>
          <td style='padding:6px 0;font-size:14px;color:#555;'>القسم:</td>
          <td style='padding:6px 0;font-size:14px;color:#1a1a2e;text-align:left;'>{department}</td>
        </tr>
        <tr>
          <td style='padding:6px 0;font-size:14px;color:#555;'>تاريخ الطلب:</td>
          <td style='padding:6px 0;font-size:14px;color:#1a1a2e;text-align:left;'>{dateStr}</td>
        </tr>
        <tr>
          <td style='padding:6px 0;font-size:15px;font-weight:700;color:#555;'>المبلغ المطلوب:</td>
          <td style='padding:6px 0;font-size:22px;font-weight:800;color:#F7941D;
                     letter-spacing:1px;text-align:left;'>{amount:N3} د.ك</td>
        </tr>
        {reasonRow}
      </table>
    </td>
  </tr>

  <tr>
    <td style='background:#fff8f0;padding:14px 28px;border-top:1px solid #ffe0b2;text-align:center;'>
      <p style='margin:0;font-size:13px;color:#e07b00;font-weight:600;'>
        يرجى مراجعة الطلب والموافقة عليه أو رفضه من لوحة التحكم
      </p>
    </td>
  </tr>

  <tr>
    <td style='background:#f7f8fa;padding:12px 28px;border-top:1px solid #eee;
               font-size:12px;color:#aaa;text-align:center;'>
      نظام معهد موس &nbsp;|&nbsp; {nowStr}
    </td>
  </tr>

</table>
</td></tr>
</table>
</body>
</html>";
    }

    public async Task SendAppointmentReminderAsync(string customerName, string? employeeName, DateTime appointmentDate, int minutesBefore)
    {
        if (string.IsNullOrWhiteSpace(_settings.SenderEmail) || _settings.SenderEmail.StartsWith("YOUR_"))
        {
            _logger.LogWarning("Email not configured — skipping appointment reminder.");
            return;
        }
        try
        {
            var whenLabel = minutesBefore switch
            {
                1440 => "قبل يوم كامل",
                120 => "قبل ساعتين",
                5 => "قبل 5 دقائق",
                _ => $"قبل {minutesBefore} دقيقة"
            };
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(_settings.NotificationEmail));
            message.Subject = $"🔔 تذكير موعد — {customerName} ({whenLabel})";
            message.Body = new TextPart("html") { Text = BuildReminderEmailBody(customerName, employeeName, appointmentDate, whenLabel) };

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_settings.SenderEmail, _settings.SenderPassword);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reminder email for appointment of {Customer}", customerName);
        }
    }

    private static string BuildReminderEmailBody(string customerName, string? employeeName, DateTime appointmentDate, string whenLabel)
    {
        var dateStr = appointmentDate.ToString("dddd dd/MM/yyyy", new System.Globalization.CultureInfo("ar-EG"));
        var timeStr = appointmentDate.ToString("hh:mm tt");
        var empRow = string.IsNullOrEmpty(employeeName) ? "" : $@"
        <tr>
          <td style='padding:6px 0;font-size:14px;color:#555;'>الموظف:</td>
          <td style='padding:6px 0;font-size:14px;color:#1a1a2e;font-weight:600;text-align:left;'>{employeeName}</td>
        </tr>";
        var nowStr = DateTime.Now.ToString("HH:mm  dd/MM/yyyy");

        return $@"<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head><meta charset='utf-8'/></head>
<body style='margin:0;padding:0;background:#f0f2f5;font-family:Segoe UI,Tahoma,Arial,sans-serif;direction:rtl;'>
<table width='100%' cellpadding='0' cellspacing='0' style='background:#f0f2f5;padding:30px 0;'>
<tr><td align='center'>
<table width='520' cellpadding='0' cellspacing='0'
       style='background:#fff;border-radius:14px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.10);'>
  <tr>
    <td style='background:linear-gradient(135deg,#1a1a2e,#0f3460);padding:22px 28px;'>
      <h2 style='margin:0;color:#F7941D;font-size:20px;'>🔔 تذكير موعد</h2>
      <p style='margin:4px 0 0;color:rgba(255,255,255,0.65);font-size:13px;'>معهد موس للرجال</p>
    </td>
  </tr>
  <tr>
    <td style='padding:18px 28px 8px;'>
      <div style='display:inline-block;background:#F7941D;color:#fff;
                  border-radius:8px;padding:8px 18px;font-size:16px;font-weight:700;'>
        {whenLabel}
      </div>
    </td>
  </tr>
  <tr>
    <td style='padding:10px 28px 20px;'>
      <table width='100%' cellpadding='0' cellspacing='0'
             style='background:#f7f8fa;border-radius:10px;padding:16px 18px;'>
        <tr>
          <td style='padding:6px 0;font-size:14px;color:#555;'>العميل:</td>
          <td style='padding:6px 0;font-size:15px;font-weight:700;color:#1a1a2e;text-align:left;'>{customerName}</td>
        </tr>
        <tr>
          <td style='padding:6px 0;font-size:14px;color:#555;'>التاريخ:</td>
          <td style='padding:6px 0;font-size:14px;color:#1a1a2e;text-align:left;'>{dateStr}</td>
        </tr>
        <tr>
          <td style='padding:6px 0;font-size:15px;font-weight:700;color:#555;'>الوقت:</td>
          <td style='padding:6px 0;font-size:22px;font-weight:800;color:#0f3460;
                     letter-spacing:2px;text-align:left;'>{timeStr}</td>
        </tr>
        {empRow}
      </table>
    </td>
  </tr>
  <tr>
    <td style='background:#f7f8fa;padding:12px 28px;border-top:1px solid #eee;
               font-size:12px;color:#aaa;text-align:center;'>
      نظام معهد موس &nbsp;|&nbsp; {nowStr}
    </td>
  </tr>
</table>
</td></tr>
</table>
</body>
</html>";
    }

    private static string BuildEmailBody(Sale sale, string cashierName)
    {
        var itemsHtml = new System.Text.StringBuilder();
        var employeeName = sale.Employee?.FullName ?? "—";

        foreach (var item in sale.SaleItems)
        {
            itemsHtml.Append($@"
            <div style='padding:10px 0;border-bottom:1px solid #eee;'>
                <div style='font-weight:600;font-size:15px;color:#1a1a2e;margin-bottom:4px;'>{item.ItemName}</div>
                <div style='font-size:13px;color:#555;'>الكمية: <strong>{item.Quantity} × {item.Price:N3} د.ك</strong></div>
                <div style='font-size:13px;color:#888;margin-top:2px;'>الموظف: {employeeName}</div>
            </div>");
        }

        var discountRow = sale.Discount > 0
            ? $"<tr><td style='padding:5px 0;font-size:14px;color:#c0392b;'>الخصم</td><td style='text-align:left;color:#c0392b;'>- {sale.Discount:N3} د.ك</td></tr>"
            : "";

        var dateStr = sale.SaleDate.ToString("MMM yyyy, HH:mm dd");

        return $@"<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head><meta charset='utf-8'/></head>
<body style='margin:0;padding:0;background:#f0f2f5;font-family:Segoe UI,Tahoma,Arial,sans-serif;direction:rtl;'>
<table width='100%' cellpadding='0' cellspacing='0' style='background:#f0f2f5;padding:30px 0;'>
<tr><td align='center'>
<table width='560' cellpadding='0' cellspacing='0'
       style='background:#fff;border-radius:14px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.10);'>

  <tr>
    <td style='background:linear-gradient(135deg,#1a1a2e,#0f3460);padding:24px 28px;'>
      <h2 style='margin:0;color:#F7941D;font-size:22px;'>عملية بيع جديدة</h2>
      <p style='margin:6px 0 0;color:rgba(255,255,255,0.7);font-size:13px;'>معهد وصالون موس للرجال</p>
    </td>
  </tr>

  <tr>
    <td style='padding:20px 28px 10px;'>
      <table width='100%' cellpadding='0' cellspacing='0'>
        <tr>
          <td style='padding:5px 0;font-size:14px;color:#555;'>رقم الفاتورة:</td>
          <td style='padding:5px 0;font-size:14px;font-weight:700;color:#1a1a2e;text-align:left;'>{sale.InvoiceNumber}</td>
        </tr>
        <tr>
          <td style='padding:5px 0;font-size:14px;color:#555;'>التاريخ:</td>
          <td style='padding:5px 0;font-size:14px;color:#1a1a2e;text-align:left;'>{dateStr}</td>
        </tr>
        <tr>
          <td style='padding:5px 0;font-size:14px;color:#555;'>القسم:</td>
          <td style='padding:5px 0;font-size:14px;color:#1a1a2e;text-align:left;'>{sale.SaleType}</td>
        </tr>
        <tr>
          <td style='padding:5px 0;font-size:14px;color:#555;'>الكاشير:</td>
          <td style='padding:5px 0;font-size:14px;color:#1a1a2e;text-align:left;'>{cashierName}</td>
        </tr>
      </table>
    </td>
  </tr>

  <tr><td style='padding:0 28px;'><hr style='border:none;border-top:2px dashed #eee;margin:0;'/></td></tr>

  <tr>
    <td style='padding:16px 28px 8px;'>
      <p style='margin:0 0 10px;font-weight:700;font-size:15px;color:#1a1a2e;'>تفاصيل الفاتورة:</p>
      {itemsHtml}
    </td>
  </tr>

  <tr>
    <td style='padding:12px 28px 20px;'>
      <table width='100%' cellpadding='0' cellspacing='0'
             style='background:#f7f8fa;border-radius:10px;padding:14px 18px;'>
        <tr>
          <td style='padding:5px 0;font-size:14px;color:#555;'>المجموع الفرعي:</td>
          <td style='font-size:14px;color:#333;text-align:left;'>{sale.TotalAmount:N3} د.ك</td>
        </tr>
        {discountRow}
        <tr>
          <td style='padding-top:8px;font-size:16px;font-weight:700;color:#1a1a2e;border-top:2px solid #ddd;'>الإجمالي:</td>
          <td style='font-size:16px;font-weight:700;color:#F7941D;text-align:left;border-top:2px solid #ddd;padding-top:8px;'>
            {sale.NetAmount:N3} د.ك
          </td>
        </tr>
      </table>
      <p style='margin:12px 0 0;font-size:13px;color:#888;'>
        طريقة الدفع: <strong style='color:#1a1a2e;'>{sale.PaymentMethod}</strong>
      </p>
    </td>
  </tr>

  <tr>
    <td style='background:#f7f8fa;padding:14px 28px;border-top:1px solid #eee;
               font-size:12px;color:#aaa;text-align:center;'>
      نظام معهد موس &nbsp;|&nbsp; شكراً لزيارتكم
    </td>
  </tr>

</table>
</td></tr>
</table>
</body>
</html>";
    }
}