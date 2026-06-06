using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Sale
    {
        public int Id { get; set; }

        [Display(Name = "Invoice No.")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [Display(Name = "Employee")]
        public int? EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [Display(Name = "Sale Date")]
        [DataType(DataType.DateTime)]
        public DateTime SaleDate { get; set; } = DateTime.Now;

        [Display(Name = "Total")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Discount")]
        [DataType(DataType.Currency)]
        public decimal Discount { get; set; }

        [Display(Name = "Net")]
        [DataType(DataType.Currency)]
        public decimal NetAmount { get; set; }

        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Cash";

        [Display(Name = "Invoice Type")]
        public string SaleType { get; set; } = "Service"; // "Service" or "Products"

        [Display(Name = "Status")]
        public string Status { get; set; } = "Completed";

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Cash Amount")]
        public decimal? CashAmount { get; set; }

        [Display(Name = "Link Amount")]
        public decimal? LinkAmount { get; set; }

        [Display(Name = "Employee Debt")]
        public int? DebtEmployeeId { get; set; }

        [ForeignKey("DebtEmployeeId")]
        public Employee? DebtEmployee { get; set; }

        [Display(Name = "Employee Gift")]
        [DataType(DataType.Currency)]
        public decimal? EmployeeGift { get; set; }

        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }

    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public Sale? Sale { get; set; }

        [Display(Name = "Service / Product")]
        public int? ServiceId { get; set; }
        public Service? Service { get; set; }

        [Display(Name = "Product")]
        public int? ProductId { get; set; }
        public Product? Product { get; set; }

        [Display(Name = "Name")]
        public string ItemName { get; set; } = string.Empty;

        [Display(Name = "Quantity")]
        public int Quantity { get; set; } = 1;

        [Display(Name = "Price")]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [Display(Name = "Total")]
        [DataType(DataType.Currency)]
        public decimal Total { get; set; }
    }
}