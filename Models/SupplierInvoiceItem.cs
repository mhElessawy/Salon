using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    // سطر منتج داخل فاتورة مورد آجلة — عند حفظ الفاتورة يُنشئ حركة استلام في المخزون ويُحدِّث
    // تكلفة شراء المنتج (متوسط مرجّح)
    public class SupplierInvoiceItem
    {
        public int Id { get; set; }

        [Required]
        public int SupplierInvoiceId { get; set; }

        [ForeignKey("SupplierInvoiceId")]
        public SupplierInvoice? SupplierInvoice { get; set; }

        // قد يكون فارغاً لو أُدخل اسم منتج غير موجود بالمخزون (لا يُنشئ حركة مخزون في هذه الحالة)
        public int? ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        [Display(Name = "المنتج")]
        public string ProductName { get; set; } = string.Empty;

        [Display(Name = "الوحدة")]
        public string? Unit { get; set; }

        [Display(Name = "الكمية")]
        public int Quantity { get; set; } = 1;

        [Display(Name = "سعر الشراء")]
        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        [Display(Name = "الخصم")]
        [DataType(DataType.Currency)]
        public decimal Discount { get; set; }

        [NotMapped]
        public decimal Total => Math.Max(0, (Quantity * UnitPrice) - Discount);
    }
}
