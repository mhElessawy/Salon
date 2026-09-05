using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    // مبلغ جزئي مصروف من عهدة معيَّنة لتمويل طلب شراء واحد — طلب الشراء ممكن يتوزَّع على أكثر
    // من عهدة (إيداع) لموظف واحد لو مفيش إيداع واحد كافي لوحده (انظر رصيد العهدة الموحَّد في
    // Services/CustodyPoolCalculator)، فبيتسجَّل صف منفصل هنا لكل إيداع اتخصم منه جزء من المبلغ.
    // تُنشأ هذه الصفوف فقط وقت مطابقة الكاشير واعتماد الطلب (PurchaseRequestsController.CashierReview).
    public class PurchaseRequestCustodyAllocation
    {
        public int Id { get; set; }

        public int PurchaseRequestId { get; set; }

        [ForeignKey("PurchaseRequestId")]
        public PurchaseRequest? PurchaseRequest { get; set; }

        public int CustodyId { get; set; }

        [ForeignKey("CustodyId")]
        public Custody? Custody { get; set; }

        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
