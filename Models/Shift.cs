using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Shift
    {
        // حالة اعتماد اليومية — منفصلة عن Status (مفتوح/مغلق) الذي يعبّر فقط عن انتهاء وقت العمل الفعلي.
        public static class ApprovalStatuses
        {
            public const string Open = "مفتوح";
            public const string Approved = "معتمد";
            public const string ApprovedWithDiscrepancy = "معتمد - يوجد فروقات";
            public const string AutoClosedUnapproved = "مغلق آلياً - بانتظار الاعتماد";
        }

        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الشفت مطلوب")]
        [Display(Name = "اسم الشفت")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "تاريخ الشفت")]
        [DataType(DataType.Date)]
        public DateTime ShiftDate { get; set; } = DateTime.Today;

        [Display(Name = "وقت البداية")]
        public TimeSpan StartTime { get; set; }

        [Display(Name = "وقت النهاية")]
        public TimeSpan? EndTime { get; set; }

        [Display(Name = "الكاشير")]
        public string? CashierName { get; set; }

        [Display(Name = "رصيد الفتح")]
        [DataType(DataType.Currency)]
        public decimal OpeningBalance { get; set; }

        [Display(Name = "رصيد الإغلاق")]
        [DataType(DataType.Currency)]
        public decimal? ClosingBalance { get; set; }

        [Display(Name = "الحالة")]
        public string Status { get; set; } = "مفتوح";

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // يميّز الصف اللي أنشأته شاشة "اعتماد اليومية" (DailyClosureService) عن صفوف "الشفتات"
        // العادية اللي بتتفتح يدوياً من شاشة إدارة الشفتات — الاتنين بيشتغلوا على صفوف منفصلة
        // تماماً في نفس الجدول حتى لا تتأثر شاشة بالتانية.
        public bool IsClosureRecord { get; set; }

        // ===== اعتماد وإغلاق اليومية =====

        [Display(Name = "حالة الاعتماد")]
        public string ApprovalStatus { get; set; } = ApprovalStatuses.Open;

        // مطابقة الصندوق
        [Display(Name = "الرصيد المتوقع")]
        public decimal? ExpectedCashBalance { get; set; }

        [Display(Name = "سبب فرق الصندوق")]
        public string? CashDifferenceReason { get; set; }

        // مطابقة جهاز KNET
        [Display(Name = "إجمالي KNET بالنظام")]
        public decimal? SystemKnetTotal { get; set; }

        [Display(Name = "إجمالي جهاز نقاط البيع")]
        public decimal? DeviceKnetTotal { get; set; }

        [Display(Name = "رقم إيصال الموازنة")]
        public string? KnetSettlementNumber { get; set; }

        [Display(Name = "سبب فرق KNET")]
        public string? KnetDifferenceReason { get; set; }

        // قائمة التحقق (Checklist)
        public bool ReviewedRevenue { get; set; }
        public bool ReviewedCash { get; set; }
        public bool ReviewedKnet { get; set; }
        public bool ReviewedExpenses { get; set; }
        public bool ReviewedWithdrawals { get; set; }
        public bool ReviewedDeposits { get; set; }
        public bool ReviewedAdvances { get; set; }
        public bool ReviewedEmployeeDebts { get; set; }
        public bool ReviewedCustody { get; set; }
        public bool ConfirmedNoDiscrepancies { get; set; }

        [Display(Name = "ملاحظات الاعتماد")]
        public string? ApprovalNotes { get; set; }

        // بيانات الاعتماد
        public string? ApprovedByUserId { get; set; }

        [Display(Name = "اعتمدها")]
        public string? ApprovedByUserName { get; set; }

        [Display(Name = "وقت الاعتماد")]
        public DateTime? ApprovedAt { get; set; }

        // الإغلاق التلقائي
        public bool IsAutoClosed { get; set; }
        public DateTime? AutoClosedAt { get; set; }

        // إعادة الفتح (آخر مرة — التاريخ الكامل مسجَّل في سجل الأنشطة)
        public string? ReopenedByUserId { get; set; }

        [Display(Name = "أعاد الفتح")]
        public string? ReopenedByUserName { get; set; }

        [Display(Name = "وقت إعادة الفتح")]
        public DateTime? ReopenedAt { get; set; }

        [Display(Name = "سبب إعادة الفتح")]
        public string? ReopenReason { get; set; }
    }
}