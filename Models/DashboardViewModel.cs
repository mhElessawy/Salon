namespace Salon.Models
{
    public class DashboardViewModel
    {
        public decimal SalesToday { get; set; }
        public int CustomersToday { get; set; }
        public decimal ExpensesToday { get; set; }
        public decimal NetProfitToday { get; set; }
        public int NewCustomersToday { get; set; }
        public List<Customer> UpcomingBirthdays { get; set; } = new();
        public List<Product> ExpiringProducts { get; set; } = new();
    }
}
