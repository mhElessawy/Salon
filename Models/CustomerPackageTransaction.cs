using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class CustomerPackageTransaction
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Customer Subscription")]
        public int CustomerPackageId { get; set; }

        [ForeignKey("CustomerPackageId")]
        public CustomerPackage? CustomerPackage { get; set; }

        [Display(Name = "Usage Date")]
        [DataType(DataType.DateTime)]
        public DateTime UsedDate { get; set; } = DateTime.Now;

        [Display(Name = "Employee")]
        public int? EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}