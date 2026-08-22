namespace Salon.Models
{
    /// <summary>ﬁÌ„… „Ê“¯⁄… ⁄·Ï «·√ﬁ”«„ «·≈Ì—«œÌ… (Õ·«ﬁ…/„”«Ã) »«·≈÷«›… ··≈Ã„«·Ì «·⁄«„.</summary>
    public class DashboardDeptValue
    {
        public decimal Haircut { get; set; }
        public decimal Massage { get; set; }
        public decimal Total { get; set; }
    }

    public class DashboardAttendanceValue
    {
        public int HaircutPresent { get; set; }
        public int HaircutTotal { get; set; }
        public int MassagePresent { get; set; }
        public int MassageTotal { get; set; }
        public int AllPresent { get; set; }
        public int AllTotal { get; set; }
    }

    public class DashboardViewModel
    {
        public bool HasAccess { get; set; } = true;

        /// <summary>‰ÿ«ﬁ «·⁄—÷ Õ”» ’·«ÕÌ… «·„” Œœ„: All (√œ„‰/„œÌ—) | Haircut | Massage (”ﬂ— Ì— ﬁ”„) | Personal („ÊŸ›)</summary>
        public string ViewScope { get; set; } = "All";
        public string? UserDepartment { get; set; }

        public DashboardAttendanceValue Attendance { get; set; } = new();
        public List<(string Department, DateTime Date)> UnapprovedClosures { get; set; } = new();

        public DashboardDeptValue Sales { get; set; } = new();
        public DashboardDeptValue CancelledSales { get; set; } = new();
        public DashboardDeptValue CancelledSalesCount { get; set; } = new();
        public DashboardDeptValue RefundedSales { get; set; } = new();
        public DashboardDeptValue RefundedSalesCount { get; set; } = new();
        public DashboardDeptValue Customers { get; set; } = new();
        public DashboardDeptValue Expenses { get; set; } = new();
        public DashboardDeptValue Advances { get; set; } = new();
        public DashboardDeptValue NetProfit { get; set; } = new();

        public int NewCustomersToday { get; set; }
        public List<Customer> UpcomingBirthdays { get; set; } = new();
        public List<Product> ExpiringProducts { get; set; } = new();
        public List<Employee> ExpiringResidencies { get; set; } = new();

        public List<Sale> InvoicesWithNotes { get; set; } = new();
    }
}