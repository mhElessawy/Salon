namespace Salon.Models
{
    public static class AppModules
    {
        public static readonly List<(string Key, string NameAr, string Icon, string Group)> All = new()
        {
            ("Dashboard",         "Dashboard",        "fas fa-th-large",         "General"),
            ("Customers",         "Customers",            "fas fa-users",            "General"),
            ("Suppliers",         "Suppliers",           "fas fa-truck",            "Definitions"),
            ("BarberInvoice",     "Barber Invoice",     "fas fa-cut",              "Sales"),
            ("MassageInvoice",    "Massage Invoice",      "fas fa-spa",              "Sales"),
            ("ProductInvoice",    "Products Invoice",    "fas fa-box",              "Sales"),
            ("Appointments",      "Appointments",           "fas fa-calendar-check",   "Sales"),
            ("Services",          "Services",            "fas fa-concierge-bell",   "Definitions"),
            ("ServiceCategories", "Service Categories",       "fas fa-tags",             "Definitions"),
            ("Packages",          "Packages",             "fas fa-box-open",         "Definitions"),
            ("Inventory",         "Inventory",            "fas fa-boxes",            "Definitions"),
            ("Shifts",            "Shifts",            "fas fa-clock",            "Sales"),
            ("Employees",         "Employees",           "fas fa-id-badge",         "Employees"),
            ("Salaries",          "Salaries",            "fas fa-money-bill-wave",  "Employees"),
            ("Advances",          "Advances",              "fas fa-hand-holding-usd", "Employees"),
            ("Attendance",        "Attendance",   "fas fa-calendar-alt",     "Employees"),
            ("Expenses",          "Expenses",          "fas fa-receipt",          "Finance"),
            ("Deposits",          "Deposits",          "fas fa-money-bill-wave",  "Finance"),
            ("Reports",           "Reports",           "fas fa-chart-bar",        "Finance"),
            ("BarberDaily",       "Daily Performance Report", "fas fa-cut",              "Finance"),
            ("Messages",          "Messages",            "fas fa-envelope",         "General"),
            ("Settings",          "Settings",          "fas fa-cog",              "Administration"),
            ("Users",             "Users",         "fas fa-user-shield",      "Administration"),
            ("Audit",             "Activity Log",        "fas fa-history",          "Administration"),
            ("Discount",          "Apply Discount",        "fas fa-percent",          "Sales"),
            ("CustomerDebt",      "Customer Debt",     "fas fa-user-minus",       "Sales"),
        };

        // Screens that support add permission
        public static readonly HashSet<string> HasAdd = new()
        {
            "Customers", "Suppliers", "Services", "ServiceCategories", "Packages", "Inventory",
            "Appointments", "Shifts", "Employees", "Salaries", "Advances", "Attendance",
            "Expenses", "Deposits", "Users", "BarberInvoice", "MassageInvoice", "ProductInvoice"
        };

        // Screens that support delete permission
        public static readonly HashSet<string> HasDelete = new()
        {
            "Customers", "Suppliers", "Services", "ServiceCategories", "Packages", "Inventory",
            "Appointments", "Shifts", "Employees", "Advances", "Expenses", "Deposits", "Users",
            "BarberInvoice", "MassageInvoice", "ProductInvoice"
        };

        // All permission keys (view + add + delete)
        public static IEnumerable<string> AllKeys()
        {
            foreach (var (Key, _, _, _) in All)
            {
                yield return Key;
                if (HasAdd.Contains(Key)) yield return Key + "Add";
                if (HasDelete.Contains(Key)) yield return Key + "Delete";
            }
        }
    }
}