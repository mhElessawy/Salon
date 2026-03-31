using System.ComponentModel.DataAnnotations;

namespace Salon.Models
{
    public class Message
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "المرسل مطلوب")]
        [Display(Name = "المرسل")]
        public string SenderId { get; set; } = string.Empty;

        [Display(Name = "المرسل إليه")]
        public string? ReceiverId { get; set; }

        [Required(ErrorMessage = "الموضوع مطلوب")]
        [Display(Name = "الموضوع")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "المحتوى مطلوب")]
        [Display(Name = "المحتوى")]
        public string Body { get; set; } = string.Empty;

        [Display(Name = "تاريخ الإرسال")]
        public DateTime SentAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        [Display(Name = "للجميع")]
        public bool IsBroadcast { get; set; } = false;
    }
}
