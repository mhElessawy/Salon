namespace Salon.Models
{
    public class NotificationItem
    {
        public string Type { get; set; } = "";         // shift-diff | shift-close | low-stock | expense | late | absent
        public string Category { get; set; } = "";     // مهمة | تشغيلية | مالية
        public string Title { get; set; } = "";
        public string SubTitle { get; set; } = "";
        public string Body { get; set; } = "";
        public string IconClass { get; set; } = "";
        public string IconBg { get; set; } = "";
        public DateTime Date { get; set; }
        public string? ActionUrl { get; set; }
        public string? ActionText { get; set; }
    }
}
