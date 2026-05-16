using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Salon.Models;

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
            _logger.LogInformation("Email: connecting to {Host}:{Port} as {Sender}",
                _settings.SmtpHost, _settings.SmtpPort, _settings.SenderEmail);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(_settings.NotificationEmail));
            message.Subject = $"عملية بيع جديدة — فاتورة {sale.InvoiceNumber}";
            message.Body = new TextPart("html") { Text = BuildEmailBody(sale, cashierName) };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            _logger.LogInformation("Email: connected. Authenticating...");
            await smtp.AuthenticateAsync(_settings.SenderEmail, _settings.SenderPassword);
            _logger.LogInformation("Email: authenticated. Sending to {To}", _settings.NotificationEmail);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
            _logger.LogInformation("Email: invoice {InvoiceNumber} sent successfully", sale.InvoiceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email FAILED for invoice {InvoiceNumber} — {Message}",
                sale.InvoiceNumber, ex.Message);
        }
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
