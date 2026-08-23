namespace Salon.Models
{
    public static class AppModules
    {
        public static readonly List<(string Key, string NameAr, string Icon, string Group)> All = new()
        {
            ("Dashboard",         "لوحة التحكم",        "fas fa-th-large",         "عام"),
            ("Customers",         "العملاء",            "fas fa-users",            "عام"),
            ("Suppliers",         "الموردين",           "fas fa-truck",            "التعريفات"),
            ("SalesInvoices",     "لينك الفواتير",      "fas fa-file-invoice",     "المبيعات"),
            ("BarberInvoice",     "فاتورة الحلاقة",     "fas fa-cut",              "المبيعات"),
            ("MassageInvoice",    "فاتورة المساج",      "fas fa-spa",              "المبيعات"),
            ("ProductInvoice",    "فاتورة المنتجات",    "fas fa-box",              "المبيعات"),
            ("Appointments",      "المواعيد",           "fas fa-calendar-check",   "المبيعات"),
            ("Services",          "الخدمات",            "fas fa-concierge-bell",   "التعريفات"),
            ("ServiceCategories", "فئات الخدمات",       "fas fa-tags",             "التعريفات"),
            ("Packages",          "الباقات",             "fas fa-box-open",         "التعريفات"),
            ("Inventory",         "المخزون",            "fas fa-boxes",            "التعريفات"),
            ("Shifts",            "الشفتات",            "fas fa-clock",            "المبيعات"),
            ("DailyClosure",      "اعتماد اليومية",     "fas fa-clipboard-check",  "المالية"),
            ("Employees",         "الموظفين",           "fas fa-id-badge",         "الموظفون"),
            ("Salaries",          "الرواتب",            "fas fa-money-bill-wave",  "الموظفون"),
            ("Advances",          "السلف",              "fas fa-hand-holding-usd", "الموظفون"),
            ("Custody",           "العهد",              "fas fa-hand-holding",     "الموظفون"),
            ("PurchaseRequest",   "طلبات الشراء",        "fas fa-shopping-cart",    "الموظفون"),
            ("Attendance",        "الحضور والانصراف",   "fas fa-calendar-alt",     "الموظفون"),
            ("Expenses",          "المصروفات",          "fas fa-receipt",          "المالية"),
            ("Deposits",          "الإيداعات",          "fas fa-money-bill-wave",  "المالية"),
            ("Withdrawals",       "السحوبات",           "fas fa-hand-holding-usd", "المالية"),
            ("Reports",           "التقارير",           "fas fa-chart-bar",        "المالية"),
            ("Messages",          "الرسائل",            "fas fa-envelope",         "عام"),
            ("Settings",          "الإعدادات",          "fas fa-cog",              "الإدارة"),
            ("Users",             "المستخدمين",         "fas fa-user-shield",      "الإدارة"),
            ("Audit",             "سجل الأنشطة",        "fas fa-history",          "الإدارة"),
            ("Discount",          "تطبيق الخصم",        "fas fa-percent",          "المبيعات"),
            ("CustomerDebt",      "دين على العميل",     "fas fa-user-minus",       "المبيعات"),
            ("SalesInvoiceEdit",  "تعديل بيانات الفاتورة (التاريخ / طريقة الدفع / الموظف)", "fas fa-edit", "المبيعات"),
            ("SalesRefund",       "استرداد مبلغ الفاتورة", "fas fa-undo-alt",      "المبيعات"),

            // ── التقارير الفردية: تتحكم في ظهور كل تقرير بالاسم في شاشة التقارير العامة ──
            ("BarberDaily",           "تقرير الأداء اليومي",              "fas fa-cut",                 "التقارير"),
            ("ReportRevenue",         "تقرير الإيرادات حسب الفترة",       "fas fa-chart-column",        "التقارير"),
            ("ReportEmployeeRevenue", "تقرير الإيراد حسب الموظف",         "fas fa-users",                "التقارير"),
            ("ReportProfitLoss",      "تقرير الأرباح والخسائر الشهري",    "fas fa-scale-balanced",      "التقارير"),
            ("ReportClosures",        "تقرير الإغلاقات اليومية",          "fas fa-calendar-days",       "التقارير"),
            ("ReportCashMovement",    "تقرير حركة الصندوق",               "fas fa-cash-register",       "التقارير"),
            ("ReportBankMovement",    "تقرير حركة البنك",                 "fas fa-university",          "التقارير"),
            ("ReportInventory",       "تقرير المخزون",                    "fas fa-boxes",                "التقارير"),
            ("ReportRefunds",         "تقرير الاستردادات",                "fas fa-undo-alt",            "التقارير"),
            ("ReportExpenses",        "تقرير المصروفات",                  "fas fa-file-invoice-dollar", "التقارير"),
            ("ReportAttendance",      "تقارير الحضور",                    "fas fa-calendar-check",      "التقارير"),
            ("ReportAdvances",        "تقرير السلف",                      "fas fa-hand-holding-usd",    "التقارير"),
            ("ReportEvaluationList",  "تقييم الموظفين",                   "fas fa-users-cog",           "التقارير"),
            ("ReportEmployeeEvaluation", "تقييم موظف واحد",               "fas fa-user-check",          "التقارير"),
            ("ReportCustody",         "تقرير العهد",                      "fas fa-hand-holding",         "التقارير"),
            ("ReportSalesByCustomer", "المبيعات على حسب العملاء",         "fas fa-user-tag",             "التقارير"),
            ("ReportShifts",          "تقارير الشفتات",                   "fas fa-file-alt",             "التقارير"),
            ("ReportConsumption",     "تقرير الاستهلاك",                  "fas fa-file-alt",             "التقارير"),
        };

        // الشاشات التي تدعم صلاحية الإضافة
        public static readonly HashSet<string> HasAdd = new()
        {
            "Customers", "Suppliers", "Services", "ServiceCategories", "Packages", "Inventory",
            "Appointments", "Shifts", "Employees", "Salaries", "Advances", "Custody", "PurchaseRequest", "Attendance",
            "Expenses", "Deposits", "Withdrawals", "Users", "BarberInvoice", "MassageInvoice", "ProductInvoice"
        };

        // الشاشات التي تدعم صلاحية الحذف
        public static readonly HashSet<string> HasDelete = new()
        {
            "Customers", "Suppliers", "Services", "ServiceCategories", "Packages", "Inventory",
            "Appointments", "Shifts", "Employees", "Advances", "Custody", "PurchaseRequest", "Expenses", "Deposits", "Withdrawals", "Users",
            "BarberInvoice", "MassageInvoice", "ProductInvoice"
        };

        // الشاشات التي تدعم صلاحية "عملائي فقط" (تقييد الموظف على عملائه المعينين له فقط)
        public static readonly HashSet<string> HasMyOnly = new()
        {
            "Customers"
        };

        // الشاشات التي تدعم صلاحية "الموافقة" (اعتماد طلب قبل تنفيذه فعلياً)
        public static readonly HashSet<string> HasApprove = new()
        {
            "PurchaseRequest"
        };

        // الشاشات التي تدعم صلاحية "السداد" (سداد سلفة موظف مباشرةً خارج خصم الراتب)
        public static readonly HashSet<string> HasRepay = new()
        {
            "Advances"
        };

        // جميع مفاتيح الصلاحيات (مشاهدة + إضافة + حذف + عملائي فقط + موافقة + سداد)
        public static IEnumerable<string> AllKeys()
        {
            foreach (var (Key, _, _, _) in All)
            {
                yield return Key;
                if (HasAdd.Contains(Key)) yield return Key + "Add";
                if (HasDelete.Contains(Key)) yield return Key + "Delete";
                if (HasMyOnly.Contains(Key)) yield return Key + "MyOnly";
                if (HasApprove.Contains(Key)) yield return Key + "Approve";
                if (HasRepay.Contains(Key)) yield return Key + "Repay";
            }
        }
    }
}