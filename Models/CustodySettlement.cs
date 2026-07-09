using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    // تسوية/إرجاع جزء أو كل العهدة — عكس التسليم: الكاش (لو نقدي) بيرجع للصندوق كإيداع
    public class CustodySettlement
    {
        public int Id { get; set; }

        [Required]
        public int CustodyId { get; set; }

        [ForeignKey("CustodyId")]
        public Custody? Custody { get; set; }

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Display(Name = "المبلغ")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Display(Name = "تاريخ الإرجاع")]
        [DataType(DataType.Date)]
        public DateTime SettlementDate { get; set; } = DateTime.Today;

        // "نقدي" (يضاف للصندوق) | "لينك" (لا يؤثر على الصندوق)
        [Display(Name = "طريقة الإرجاع")]
        public string PaymentMethod { get; set; } = "نقدي";

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        // سجل الإيداع المرتبط تلقائياً (عند الإرجاع نقداً) — يُنشأ عند التسوية ويُحذف معها
        public int? DepositId { get; set; }

        [ForeignKey("DepositId")]
        public Deposit? Deposit { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
