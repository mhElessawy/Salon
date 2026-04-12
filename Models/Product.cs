using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        [Display(Name = "اسم المنتج")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "الفئة")]
        public string? Category { get; set; }

        [Display(Name = "المورد")]
        public int? SupplierId { get; set; }

        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }

        [Display(Name = "سعر الشراء")]
        [DataType(DataType.Currency)]
        public decimal PurchasePrice { get; set; }

        [Display(Name = "سعر البيع")]
        [DataType(DataType.Currency)]
        public decimal SalePrice { get; set; }

        [Display(Name = "الكمية الافتتاحية")]
        public int OpeningQuantity { get; set; }

        [Display(Name = "الكمية في المخزون")]
        public int StockQuantity { get; set; }

        [Display(Name = "الحد الأدنى للمخزون")]
        public int MinStockLevel { get; set; } = 5;

        [Display(Name = "تاريخ الانتهاء")]
        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        [Display(Name = "الملاحظات")]
        public string? Notes { get; set; }

        // محتفظ بيه للتوافق مع الكود القديم
        public string? Barcode { get; set; }
        public string? Unit { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
        public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    }
}