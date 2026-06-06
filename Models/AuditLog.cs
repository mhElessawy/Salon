namespace Salon.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;        // Add / Edit / Delete / Login / Pay / Approve
        public string Module { get; set; } = string.Empty;        // Employees / Salaries / Advances / etc.
        public string? Description { get; set; }
        public int? EntityId { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}