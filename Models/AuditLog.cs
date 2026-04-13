namespace Salon.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;        // إضافة / تعديل / حذف / تسجيل دخول / صرف / موافقة
        public string Module { get; set; } = string.Empty;        // الموظفين / الرواتب / السلف / etc.
        public string? Description { get; set; }
        public int? EntityId { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
