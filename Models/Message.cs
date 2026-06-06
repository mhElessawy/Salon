using System.ComponentModel.DataAnnotations;

namespace Salon.Models
{
    public class Message
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Sender is required")]
        [Display(Name = "Sender")]
        public string SenderId { get; set; } = string.Empty;

        [Display(Name = "Recipient")]
        public string? ReceiverId { get; set; }

        [Required(ErrorMessage = "Subject is required")]
        [Display(Name = "Subject")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Content is required")]
        [Display(Name = "Content")]
        public string Body { get; set; } = string.Empty;

        [Display(Name = "Send Date")]
        public DateTime SentAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        [Display(Name = "For All")]
        public bool IsBroadcast { get; set; } = false;
    }
}
