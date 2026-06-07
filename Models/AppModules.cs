namespace Salon.Models
{
    public static class AppModules
    {
        public static readonly List<(string Key, string NameAr, string Icon, string Group)> All = new()
        {
            ("Dashboard",         "لوحة التحكم",        "fas fa-th-large",         "عام"),
            ("Customers",         "العملاء",            "fas fa-users",            "عام"),
            ("Suppliers",         "الموردين",           "fas fa-truck",            "التعريفات"),
            ("BarberInvoice",     "فاتورة الحلاقة",     "fas fa-cut",              "المبيعات"),
            ("MassageInvoice",    "فاتورة المساج",      "fas fa-spa",              "المبيعات"),
            ("ProductInvoice",    "فاتورة المنتجات",    "fas fa-box",              "المبيعات"),
            ("Appointments",      "المواعيد",           "fas fa-calendar-check",   "المبيعات"),
            ("Services",          "الخدمات",            "fas fa-concierge-bell",   "التعريفات"),
            ("ServiceCategories", "فئات الخدمات",       "fas fa-tags",             "التعريفات"),
            ("Packages",          "الباقات",             "fas fa-box-open",         "التعريفات"),
            ("Inventory",         "المخزون",            "fas fa-boxes",            "التعريفات"),
            ("Shifts",            "الشفتات",            "fas fa-clock",            "المبيعات"),
            ("Employees",         "الموظفين",           "fas fa-id-badge",         "الموظفون"),
            ("Salaries",          "الرواتب",            "fas fa-money-bill-wave",  "الموظفون"),
            ("Advances",          "السلف",              "fas fa-hand-holding-usd", "الموظفون"),
            ("Attendance",        "الحضور والانصراف",   "fas fa-calendar-alt",     "الموظفون"),
            ("Expenses",          "المصروفات",          "fas fa-receipt",          "المالية"),
            ("Deposits",          "الإيداعات",          "fas fa-money-bill-wave",  "المالية"),
            ("Withdrawals",       "السحوبات",           "fas fa-hand-holding-usd", "المالية"),
            ("Reports",           "التقارير",           "fas fa-chart-bar",        "المالية"),
            ("BarberDaily",       "تقرير الأداء اليومي", "fas fa-cut",              "المالية"),
            ("Messages",          "الرسائل",            "fas fa-envelope",         "عام"),
            ("Settings",          "الإعدادات",          "fas fa-cog",              "الإدارة"),
            ("Users",             "المستخدمين",         "fas fa-user-shield",      "الإدارة"),
            ("Audit",             "سجل الأنشطة",        "fas fa-history",          "الإدارة"),
            ("Discount",          "تطبيق الخصم",        "fas fa-percent",          "المبيعات"),
            ("CustomerDebt",      "دين على العميل",     "fas fa-user-minus",       "المبيعات"),
        };

        // الشاشات التي تدعم صلاحية الإضافة
        public static readonly HashSet<string> HasAdd = new()
        {
            "Customers", "Suppliers", "Services", "ServiceCategories", "Packages", "Inventory",
            "Appointments", "Shifts", "Employees", "Salaries", "Advances", "Attendance",
            "Expenses", "Deposits", "Withdrawals", "Users", "BarberInvoice", "MassageInvoice", "ProductInvoice"
        };

        // الشاشات التي تدعم صلاحية الحذف
        public static readonly HashSet<string> HasDelete = new()
        {
            "Customers", "Suppliers", "Services", "ServiceCategories", "Packages", "Inventory",
            "Appointments", "Shifts", "Employees", "Advances", "Expenses", "Deposits", "Withdrawals", "Users",
            "BarberInvoice", "MassageInvoice", "ProductInvoice"
        };

        // جميع مفاتيح الصلاحيات (مشاهدة + إضافة + حذف)
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