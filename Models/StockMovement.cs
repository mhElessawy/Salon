using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class StockMovement
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "المنتج")]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        /// <summary>استلام | إهلاك | بيع | استهلاك</summary>
        [Display(Name = "نوع الحركة")]
        public string MovementType { get; set; } = "استلام";

        [Required]
        [Display(Name = "الكمية")]
        public int Quantity { get; set; }

        [Display(Name = "سعر الوحدة")]
        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        [Display(Name = "الموظف")]
        public int? EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [Display(Name = "المورد")]
        public int? SupplierId { get; set; }

        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }

        [Display(Name = "السبب / الملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "تاريخ الحركة")]
        [DataType(DataType.Date)]
        public DateTime MovementDate { get; set; } = DateTime.Today;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
