using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class PurchaseRequestItem
    {
        public int Id { get; set; }

        [Required]
        public int PurchaseRequestId { get; set; }

        [ForeignKey("PurchaseRequestId")]
        public PurchaseRequest? PurchaseRequest { get; set; }

        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        [Display(Name = "المنتج")]
        public string ProductName { get; set; } = string.Empty;

        [Display(Name = "الكمية")]
        public int Quantity { get; set; } = 1;
    }
}
