using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Product name is required")]
        [Display(Name = "Product Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Category")]
        public string? Category { get; set; }

        [Display(Name = "Supplier")]
        public int? SupplierId { get; set; }

        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }

        [Display(Name = "Purchase Price")]
        [DataType(DataType.Currency)]
        public decimal PurchasePrice { get; set; }

        [Display(Name = "Sale Price")]
        [DataType(DataType.Currency)]
        public decimal SalePrice { get; set; }

        [Display(Name = "Opening Quantity")]
        public int OpeningQuantity { get; set; }

        [Display(Name = "Quantity in Stock")]
        public int StockQuantity { get; set; }

        [Display(Name = "Minimum Stock Level")]
        public int MinStockLevel { get; set; } = 5;

        [Display(Name = "Expiry Date")]
        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // Kept for backward compatibility
        public string? Barcode { get; set; }
        public string? Unit { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
        public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    }
}