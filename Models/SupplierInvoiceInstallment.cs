using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    // موعد ومبلغ دفعة مخطَّطة من فاتورة مورد آجلة — معلوماتي فقط لتحديد الاستحقاق، السداد الفعلي
    // يُسجَّل في SupplierInvoicePayment وليس مرتبطاً بدفعة مخطَّطة بعينها
    public class SupplierInvoiceInstallment
    {
        public int Id { get; set; }

        [Required]
        public int SupplierInvoiceId { get; set; }

        [ForeignKey("SupplierInvoiceId")]
        public SupplierInvoice? SupplierInvoice { get; set; }

        [Display(Name = "رقم الدفعة")]
        public int SequenceNo { get; set; }

        [Required(ErrorMessage = "مبلغ الدفعة مطلوب")]
        [Display(Name = "المبلغ")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "تاريخ الاستحقاق مطلوب")]
        [Display(Name = "تاريخ الاستحقاق")]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; } = DateTime.Today;
    }
}
