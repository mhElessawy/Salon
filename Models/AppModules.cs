namespace Salon.Models
{
    /// <summary>قائمة الشاشات/الوحدات في النظام لاستخدامها في إدارة الصلاحيات</summary>
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
            ("Inventory",         "المخزون",            "fas fa-boxes",            "التعريفات"),
            ("Shifts",            "الشفتات",            "fas fa-clock",            "المبيعات"),
            ("Employees",         "الموظفين",           "fas fa-id-badge",         "الموظفون"),
            ("Salaries",          "الرواتب",            "fas fa-money-bill-wave",  "الموظفون"),
            ("Advances",          "السلف",              "fas fa-hand-holding-usd", "الموظفون"),
            ("Attendance",        "الحضور والانصراف",   "fas fa-calendar-alt",     "الموظفون"),
            ("Expenses",          "المصروفات",          "fas fa-receipt",          "المالية"),
            ("Reports",           "التقارير",           "fas fa-chart-bar",        "المالية"),
            ("BarberDaily",       "تقرير الأداء اليومي", "fas fa-cut",              "المالية"),
            ("Messages",          "الرسائل",            "fas fa-envelope",         "عام"),
            ("Settings",          "الإعدادات",          "fas fa-cog",              "الإدارة"),
            ("Users",             "المستخدمين",         "fas fa-user-shield",      "الإدارة"),
            ("Audit",             "سجل الأنشطة",        "fas fa-history",          "الإدارة"),
        };
    }
}