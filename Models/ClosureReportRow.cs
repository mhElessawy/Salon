namespace Salon.Models
{
    public class ClosureReportRow
    {
        public int ShiftId { get; set; }
        public DateTime Date { get; set; }
        public string ApprovalStatus { get; set; } = string.Empty;
        public string? CashierName { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal? ExpectedCashBalance { get; set; }
        public decimal? ActualCashBalance { get; set; }
        public decimal? CashDifference => ExpectedCashBalance.HasValue && ActualCashBalance.HasValue
            ? ActualCashBalance - ExpectedCashBalance : null;
        public decimal? SystemKnetTotal { get; set; }
        public decimal? DeviceKnetTotal { get; set; }
        public decimal? KnetDifference => SystemKnetTotal.HasValue && DeviceKnetTotal.HasValue
            ? DeviceKnetTotal - SystemKnetTotal : null;
        public string? CashDifferenceReason { get; set; }
        public string? KnetDifferenceReason { get; set; }
        public string? ApprovedByUserName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public bool HasDiscrepancy => (CashDifference.HasValue && Math.Abs(CashDifference.Value) >= 0.001m)
            || (KnetDifference.HasValue && Math.Abs(KnetDifference.Value) >= 0.001m);
    }
}
