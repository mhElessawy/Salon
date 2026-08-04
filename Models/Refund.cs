using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    // حركة استرداد مبلغ عن فاتورة مدفوعة (كلي أو جزئي) — لا تحذف الفاتورة أبداً، فقط تُسجَّل
    // كحركة مالية عكسية مرتبطة بها وتُحدَّث حالة الفاتورة (Sale.Status) تبعاً لإجمالي المسترد.
    public class Refund
    {
        public static class Reasons
        {
            public const string CustomerCancellation = "إلغاء من العميل";
            public const string InvoiceError = "خطأ في الفاتورة";
            public const string ServiceNotProvided = "تعذر تقديم الخدمة";
            public const string Other = "سبب آخر";
        }

        public static class Methods
        {
            public const string Cash = "كاش";
            public const string Knet = "كي نت";
            public const string BankTransfer = "تحويل بنكي";
        }

        public int Id { get; set; }

        [Display(Name = "الفاتورة")]
        public int SaleId { get; set; }

        [ForeignKey("SaleId")]
        public Sale? Sale { get; set; }

        [Display(Name = "المبلغ المسترد")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Display(Name = "سبب الاسترداد")]
        public string Reason { get; set; } = Reasons.Other;

        [Display(Name = "تفاصيل السبب")]
        public string? ReasonDetails { get; set; }

        [Display(Name = "طريقة الاسترداد")]
        public string RefundMethod { get; set; } = Methods.Cash;

        [Display(Name = "تاريخ الاسترداد")]
        [DataType(DataType.DateTime)]
        public DateTime RefundDate { get; set; } = DateTime.Now;

        [Display(Name = "استرده")]
        public string? RefundedByUserId { get; set; }

        [Display(Name = "استرده")]
        public string? RefundedByUserName { get; set; }
    }
}