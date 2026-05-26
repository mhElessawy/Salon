using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class CustomerPackage
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "العميل")]
        public int CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [Required]
        [Display(Name = "الباقة")]
        public int ServicePackageId { get; set; }

        [ForeignKey("ServicePackageId")]
        public ServicePackage? ServicePackage { get; set; }

        [Display(Name = "تاريخ الشراء")]
        [DataType(DataType.Date)]
        public DateTime PurchaseDate { get; set; } = DateTime.Today;

        [Display(Name = "تاريخ الانتهاء")]
        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        [Display(Name = "إجمالي الجلسات")]
        public int TotalSessions { get; set; }

        [Display(Name = "الجلسات المتبقية")]
        public int RemainingSessions { get; set; }

        [Display(Name = "السعر المدفوع")]
        [DataType(DataType.Currency)]
        public decimal PricePaid { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "مفعّل")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<CustomerPackageTransaction> Transactions { get; set; } = new List<CustomerPackageTransaction>();
    }
}