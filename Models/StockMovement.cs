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

        [Required(ErrorMessage = "نوع الحركة مطلوب")]
        [Display(Name = "نوع الحركة")]
        public string MovementType { get; set; } = "وارد"; // وارد / صادر / تعديل

        [Required(ErrorMessage = "الكمية مطلوبة")]
        [Display(Name = "الكمية")]
        public int Quantity { get; set; }

        [Display(Name = "الكمية قبل الحركة")]
        public int QuantityBefore { get; set; }

        [Display(Name = "الكمية بعد الحركة")]
        public int QuantityAfter { get; set; }

        [Display(Name = "المرجع")]
        public string? Reference { get; set; }

        [Display(Name = "السبب")]
        public string? Reason { get; set; }

        [Display(Name = "تاريخ الحركة")]
        [DataType(DataType.Date)]
        public DateTime MovementDate { get; set; } = DateTime.Today;

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
