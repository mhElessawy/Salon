using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    // سداد فعلي لدفعة من فاتورة مورد آجلة — ينقص من مصدر الدفع المختار فقط وقت السداد
    public class SupplierInvoicePayment
    {
        public static class Sources
        {
            public const string Cash = "الصندوق";
            public const string Bank = "البنك";
            public const string Custody = "عهدة";
        }

        public int Id { get; set; }

        [Required]
        public int SupplierInvoiceId { get; set; }

        [ForeignKey("SupplierInvoiceId")]
        public SupplierInvoice? SupplierInvoice { get; set; }

        [Required(ErrorMessage = "مبلغ الدفعة مطلوب")]
        [Display(Name = "المبلغ")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Display(Name = "تاريخ الدفع")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "مصدر الدفع")]
        public string Source { get; set; } = Sources.Cash;

        // العهدة المستخدمة للسداد — مطلوبة فقط لما Source = عهدة
        public int? CustodyId { get; set; }

        [ForeignKey("CustodyId")]
        public Custody? Custody { get; set; }

        [Display(Name = "رقم المرجع")]
        public string? ReferenceNumber { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        // المصروف المرتبط تلقائياً على الصندوق أو البنك (لا يُنشأ لما Source = عهدة)
        public int? ExpenseId { get; set; }

        [ForeignKey("ExpenseId")]
        public Expense? Expense { get; set; }

        public string? PaidByUserId { get; set; }

        [Display(Name = "منفذ الدفع")]
        public string? PaidByName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
