using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    // اتفاقية بيع باقة إلكترونية: يتم إنشاؤها مرة واحدة لحظة بيع الباقة (وليس عند الاستخدام
    // أو الدفعات اللاحقة)، وتحتفظ بصورة كاملة (Snapshot) لبيانات العميل/الباقة ونص الشروط
    // كما كانت وقت التوقيع، حتى لو تغيّرت بيانات العميل أو نص الشروط في الإعدادات لاحقاً.
    public class PackageAgreement
    {
        // نص الشروط والأحكام الافتراضي — يُستخدم حتى يقوم صاحب الصالون بتعديله من صفحة
        // الإعدادات (AppSettings: PackageAgreementTermsAr / PackageAgreementTermsEn)
        public const string DefaultTermsAr =
            "يقر العميل بأن سعر الباقة هو سعر تفضيلي خاص يقل عن مجموع أسعار الجلسات عند شرائها بشكل منفصل، وذلك مقابل الاشتراك في الباقة كاملة.\n\n" +
            "وفي حال طلب العميل إلغاء الباقة قبل استكمال جميع جلساتها، يتم أولاً إعادة احتساب قيمة الجلسات المستخدمة وفق الأسعار الفردية المعتمدة لدى المنشأة بتاريخ شراء الباقة، ثم تخصم تلك القيمة من إجمالي المبلغ المدفوع، ويُرد للعميل – إن وجد – الرصيد المتبقي بعد الخصم، وذلك وفق سياسة المنشأة والأنظمة واللوائح المعمول بها.";

        public const string DefaultTermsEn =
            "The customer acknowledges that the package price is a special preferential price, lower than the total price of the sessions if purchased individually, in return for subscribing to the full package.\n\n" +
            "If the customer requests to cancel the package before completing all of its sessions, the value of the sessions already used will first be recalculated according to the individual prices in effect at the facility on the date the package was purchased. That value will then be deducted from the total amount paid, and any remaining balance — if any — will be refunded to the customer, in accordance with the facility's policy and the applicable laws and regulations.";

        public int Id { get; set; }

        [Required]
        public int CustomerPackageId { get; set; }

        [ForeignKey("CustomerPackageId")]
        public CustomerPackage? CustomerPackage { get; set; }

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

        [Display(Name = "الفاتورة")]
        public int? SaleId { get; set; }

        [ForeignKey("SaleId")]
        public Sale? Sale { get; set; }

        // ── بيانات العميل والباقة وقت التوقيع (Snapshot) ──
        [Display(Name = "اسم العميل")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "رقم الهاتف")]
        public string? CustomerPhone { get; set; }

        [Display(Name = "اسم الباقة")]
        public string PackageNameAr { get; set; } = string.Empty;

        public string? PackageNameEn { get; set; }

        [Display(Name = "عدد الجلسات")]
        public int SessionCount { get; set; }

        [Display(Name = "قيمة الباقة")]
        [DataType(DataType.Currency)]
        public decimal PackagePrice { get; set; }

        [Display(Name = "المبلغ المدفوع")]
        [DataType(DataType.Currency)]
        public decimal AmountPaid { get; set; }

        [Display(Name = "تاريخ الشراء")]
        public DateTime PurchaseDate { get; set; }

        [Display(Name = "تاريخ الانتهاء")]
        public DateTime? ExpiryDate { get; set; }

        [Display(Name = "طريقة الدفع")]
        public string PaymentMethod { get; set; } = string.Empty;

        [Display(Name = "رقم الفاتورة")]
        public string InvoiceNumber { get; set; } = string.Empty;

        // ── نص الشروط والأحكام وقت التوقيع (Snapshot من صفحة الإعدادات) ──
        public string TermsAr { get; set; } = string.Empty;
        public string TermsEn { get; set; } = string.Empty;

        // ── التوقيع الإلكتروني ──
        [Display(Name = "صورة التوقيع")]
        public string SignatureImage { get; set; } = string.Empty; // Base64 data URL (PNG)

        [Display(Name = "تاريخ ووقت التوقيع")]
        public DateTime SignedAt { get; set; } = DateTime.Now;

        public string? SignedByUserId { get; set; }

        [Display(Name = "المستخدم")]
        public string SignedByUserName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}