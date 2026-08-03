using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;
using Salon.Services;

var builder = WebApplication.CreateBuilder(args);

// تحميل appsettings.Local.json لو موجود (مش متتبّع في git — انظر .gitignore). بيسمح
// لأي مطوّر إنه يشغّل المشروع لوكل على قاعدة بيانات السيرفر الحقيقية من غير ما يحط
// أي باسورد جوا ملف متتبّع بالغلط.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add EF Core - Use SQLite on Linux, SQL Server on Windows
var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connStr))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not set. Provide it via the " +
        "ConnectionStrings__DefaultConnection environment variable (or appsettings.Production.json on the server).");
}
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (connStr.StartsWith("Data Source=") || connStr.EndsWith(".db"))
        options.UseSqlite(connStr);
    else
        options.UseSqlServer(connStr, o => o.UseCompatibilityLevel(120));
});

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ?????? ?? SecurityStamp ?? ?? ??? (???? ??? ?????? ??????? ?????)
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

// Cookie configuration
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    // ????? ??????? ????? ?????? ??? ?????? ?????? ?? ?????
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

// Add session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IDailyClosureService, DailyClosureService>();

// Email service
var emailSettings = builder.Configuration.GetSection("EmailSettings").Get<Salon.Services.EmailSettings>()
    ?? new Salon.Services.EmailSettings();
builder.Services.AddSingleton(emailSettings);
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<Salon.Services.ReminderWorker>();
builder.Services.AddHostedService<Salon.Services.DailyClosureAutoCloseWorker>();

builder.Services.AddMemoryCache();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Seed default admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated();

        var isSqlite = context.Database.ProviderName?.Contains("Sqlite") == true;

        // ????? ????? ???????? ???????? (????? ?????/????? ?????)
        void TryExec(string sql) { try { context.Database.ExecuteSqlRaw(sql); } catch { } }

        if (isSqlite)
        {
            TryExec("ALTER TABLE EmployeeAdvances ADD COLUMN AmountPaid REAL NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE EmployeeAdvances ADD COLUMN ManagerId TEXT NULL");
            TryExec("ALTER TABLE EmployeeAdvances ADD COLUMN ManagerName TEXT NULL");
            TryExec("ALTER TABLE EmployeeAdvances ADD COLUMN DecisionAt TEXT NULL");
            TryExec("ALTER TABLE EmployeeAdvances ADD COLUMN RejectionReason TEXT NULL");
            TryExec("ALTER TABLE EmployeeAdvances ADD COLUMN CashierId TEXT NULL");
            TryExec("ALTER TABLE EmployeeAdvances ADD COLUMN CashierName TEXT NULL");
            TryExec("ALTER TABLE EmployeeAdvances ADD COLUMN DisbursedAt TEXT NULL");
            TryExec("ALTER TABLE EmployeeAdvances ADD COLUMN TransferReference TEXT NULL");
            TryExec("ALTER TABLE EmployeeAdvances ADD COLUMN TransferBank TEXT NULL");
            TryExec("ALTER TABLE EmployeeAdvances ADD COLUMN TransferNotes TEXT NULL");
            TryExec("ALTER TABLE EmployeeAdvances ADD COLUMN TransferredById TEXT NULL");
            TryExec("ALTER TABLE EmployeeAdvances ADD COLUMN TransferredByName TEXT NULL");
            // ترحيل الحالات القديمة (معلق/موافق عليها) لأسماء حالات مسار الموافقة/الصرف الجديد
            TryExec("UPDATE EmployeeAdvances SET Status = 'بانتظار موافقة المدير' WHERE Status = 'معلق'");
            TryExec("UPDATE EmployeeAdvances SET Status = 'تم الصرف' WHERE Status = 'موافق عليها' AND PaymentMethod = 'نقدي'");
            TryExec("UPDATE EmployeeAdvances SET Status = 'تم التحويل' WHERE Status = 'موافق عليها' AND PaymentMethod <> 'نقدي'");
            TryExec("ALTER TABLE Products ADD COLUMN SupplierId INTEGER NULL");
            TryExec("ALTER TABLE Products ADD COLUMN OpeningQuantity INTEGER NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Attendances ADD COLUMN QueuePosition INTEGER NULL");
            TryExec("ALTER TABLE Sales ADD COLUMN KnetReceiptNumber TEXT NULL");
            TryExec("ALTER TABLE Sales ADD COLUMN EmployeeGift REAL NULL");
            TryExec("ALTER TABLE Sales ADD COLUMN GiftForEmployee REAL NULL");
            TryExec("ALTER TABLE Sales ADD COLUMN PaidAmount REAL NULL");
            TryExec("ALTER TABLE Sales ADD COLUMN ChangeAmount REAL NULL");
            TryExec("ALTER TABLE Sales ADD COLUMN CreatedByUserId TEXT NULL");
            TryExec("ALTER TABLE Sales ADD COLUMN CreatedByUserName TEXT NULL");
            TryExec("ALTER TABLE Salaries ADD COLUMN GiftAmount REAL NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Salaries ADD COLUMN HadiyaAmount REAL NULL");
            TryExec("ALTER TABLE Salaries ADD COLUMN CommissionAmount REAL NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Salaries ADD COLUMN PaymentMethod TEXT NOT NULL DEFAULT '����'");
            TryExec("ALTER TABLE Salaries ADD COLUMN EmployeeDebtDeducted REAL NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Employees ADD COLUMN SalesTarget REAL NULL");
            TryExec("ALTER TABLE Employees ADD COLUMN CommissionAfterTarget REAL NULL");
            TryExec("ALTER TABLE Employees ADD COLUMN RevenueDepartment TEXT NULL");
            TryExec(@"CREATE TABLE IF NOT EXISTS Suppliers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Phone TEXT,
                Email TEXT,
                Address TEXT,
                Notes TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')))");
            TryExec(@"CREATE TABLE IF NOT EXISTS StockMovements (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductId INTEGER NOT NULL,
                MovementType TEXT NOT NULL DEFAULT '??????',
                Quantity INTEGER NOT NULL,
                UnitPrice REAL NOT NULL DEFAULT 0,
                EmployeeId INTEGER NULL,
                SupplierId INTEGER NULL,
                Notes TEXT,
                MovementDate TEXT NOT NULL DEFAULT (date('now')),
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')))");
            TryExec(@"CREATE TABLE IF NOT EXISTS AttendancePermissions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AttendanceId INTEGER NOT NULL,
                LeaveTime TEXT NOT NULL,
                ReturnTime TEXT NULL,
                Notes TEXT,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (AttendanceId) REFERENCES Attendances(Id) ON DELETE CASCADE)");
            TryExec(@"CREATE TABLE IF NOT EXISTS ServicePackages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                NameAr TEXT NOT NULL,
                NameEn TEXT,
                SessionCount INTEGER NOT NULL DEFAULT 4,
                Price REAL NOT NULL DEFAULT 0,
                ValidityDays INTEGER NOT NULL DEFAULT 90,
                Description TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                ServiceCategoryId INTEGER NULL,
                FOREIGN KEY (ServiceCategoryId) REFERENCES ServiceCategories(Id) ON DELETE SET NULL)");
            TryExec(@"CREATE TABLE IF NOT EXISTS CustomerPackages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerId INTEGER NOT NULL,
                ServicePackageId INTEGER NOT NULL,
                PurchaseDate TEXT NOT NULL DEFAULT (date('now')),
                ExpiryDate TEXT NULL,
                TotalSessions INTEGER NOT NULL DEFAULT 0,
                RemainingSessions INTEGER NOT NULL DEFAULT 0,
                PricePaid REAL NOT NULL DEFAULT 0,
                Notes TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
                FOREIGN KEY (ServicePackageId) REFERENCES ServicePackages(Id))");
            TryExec(@"CREATE TABLE IF NOT EXISTS CustomerPackageTransactions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerPackageId INTEGER NOT NULL,
                UsedDate TEXT NOT NULL DEFAULT (datetime('now')),
                EmployeeId INTEGER NULL,
                Notes TEXT,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (CustomerPackageId) REFERENCES CustomerPackages(Id) ON DELETE CASCADE,
                FOREIGN KEY (EmployeeId) REFERENCES Employees(Id) ON DELETE SET NULL)");
            TryExec(@"CREATE TABLE IF NOT EXISTS Deposits (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Amount REAL NOT NULL DEFAULT 0,
                Description TEXT NOT NULL,
                Source TEXT,
                DepositDate TEXT NOT NULL DEFAULT (date('now')),
                Notes TEXT,
                Department TEXT NOT NULL DEFAULT 'حلاقة',
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')))");
            TryExec("ALTER TABLE Deposits ADD COLUMN Department TEXT NOT NULL DEFAULT 'حلاقة'");
            TryExec("ALTER TABLE Deposits ADD COLUMN PaymentMethod TEXT NOT NULL DEFAULT 'نقدي'");
            TryExec("ALTER TABLE Appointments ADD COLUMN EndTime TEXT NULL");
            TryExec("ALTER TABLE Appointments ADD COLUMN CustomerPackageId INTEGER NULL");
            TryExec("ALTER TABLE Customers ADD COLUMN AssignedEmployeeId INTEGER NULL");
            TryExec(@"CREATE TABLE IF NOT EXISTS Withdrawals (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Amount REAL NOT NULL DEFAULT 0,
                Description TEXT NOT NULL,
                Reason TEXT,
                WithdrawalDate TEXT NOT NULL DEFAULT (date('now')),
                Notes TEXT,
                Department TEXT NULL,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')))");
            TryExec("ALTER TABLE Withdrawals ADD COLUMN Department TEXT NULL");
            TryExec("ALTER TABLE Withdrawals ADD COLUMN PaymentMethod TEXT NOT NULL DEFAULT 'نقدي'");
            TryExec(@"CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL DEFAULT '')");
            TryExec(@"CREATE TABLE IF NOT EXISTS AppointmentReminders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AppointmentId INTEGER NOT NULL,
                MinutesBefore INTEGER NOT NULL DEFAULT 60,
                IsSent INTEGER NOT NULL DEFAULT 0,
                SentAt TEXT NULL,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id) ON DELETE CASCADE)");
            TryExec(@"CREATE TABLE IF NOT EXISTS Custodies (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EmployeeId INTEGER NOT NULL,
                Amount REAL NOT NULL DEFAULT 0,
                CustodyDate TEXT NOT NULL DEFAULT (date('now')),
                PaymentMethod TEXT NOT NULL DEFAULT 'نقدي',
                Reason TEXT,
                Notes TEXT,
                ExpenseId INTEGER NULL,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (EmployeeId) REFERENCES Employees(Id),
                FOREIGN KEY (ExpenseId) REFERENCES Expenses(Id))");
            TryExec("ALTER TABLE Custodies ADD COLUMN ExpenseId INTEGER NULL");
            TryExec("ALTER TABLE Expenses ADD COLUMN EmployeeId INTEGER NULL");
            // نظام تسوية/إرجاع العهد القديم أُلغي بالكامل واستُبدل بتدفق طلبات الشراء أدناه
            TryExec("DROP TABLE IF EXISTS CustodySettlements");
            TryExec(@"CREATE TABLE IF NOT EXISTS PurchaseRequests (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EmployeeId INTEGER NOT NULL,
                CustodyId INTEGER NOT NULL DEFAULT 0,
                RequestDate TEXT NOT NULL DEFAULT (date('now')),
                Reason TEXT NOT NULL DEFAULT '',
                SupplierId INTEGER NULL,
                EstimatedAmount REAL NOT NULL DEFAULT 0,
                Notes TEXT,
                Status TEXT NOT NULL DEFAULT 'بانتظار موافقة المدير',
                RejectionReason TEXT,
                ApprovedByUserId TEXT NULL,
                ApprovedByName TEXT NULL,
                ApprovedAt TEXT NULL,
                InvoiceNumber TEXT NULL,
                ActualAmount REAL NULL,
                InvoicePhotoPath TEXT NULL,
                ReviewedByUserId TEXT NULL,
                ReviewedByName TEXT NULL,
                ReviewedAt TEXT NULL,
                ExpenseId INTEGER NULL,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (EmployeeId) REFERENCES Employees(Id),
                FOREIGN KEY (CustodyId) REFERENCES Custodies(Id),
                FOREIGN KEY (SupplierId) REFERENCES Suppliers(Id),
                FOREIGN KEY (ExpenseId) REFERENCES Expenses(Id))");
            TryExec("ALTER TABLE PurchaseRequests ADD COLUMN CustodyId INTEGER NOT NULL DEFAULT 0");
            TryExec(@"CREATE TABLE IF NOT EXISTS PurchaseRequestItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PurchaseRequestId INTEGER NOT NULL,
                ProductName TEXT NOT NULL,
                Quantity INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (PurchaseRequestId) REFERENCES PurchaseRequests(Id) ON DELETE CASCADE)");
            // يمنع تكرار رقم الفاتورة عند طلبين متزامنين (double-click / إعادة إرسال) — لو فيه
            // تكرار موجود بالفعل في البيانات هيفشل بصمت (TryExec) لحد ما يتصلّح يدويًا
            TryExec("CREATE UNIQUE INDEX IF NOT EXISTS IX_Sales_InvoiceNumber ON Sales(InvoiceNumber) WHERE InvoiceNumber IS NOT NULL AND InvoiceNumber <> ''");

            // نظام اعتماد وإغلاق اليومية
            TryExec("ALTER TABLE Shifts ADD COLUMN ApprovalStatus TEXT NOT NULL DEFAULT 'مفتوح'");
            TryExec("ALTER TABLE Shifts ADD COLUMN ExpectedCashBalance REAL NULL");
            TryExec("ALTER TABLE Shifts ADD COLUMN CashDifferenceReason TEXT NULL");
            TryExec("ALTER TABLE Shifts ADD COLUMN SystemKnetTotal REAL NULL");
            TryExec("ALTER TABLE Shifts ADD COLUMN DeviceKnetTotal REAL NULL");
            TryExec("ALTER TABLE Shifts ADD COLUMN KnetSettlementNumber TEXT NULL");
            TryExec("ALTER TABLE Shifts ADD COLUMN KnetDifferenceReason TEXT NULL");
            TryExec("ALTER TABLE Shifts ADD COLUMN ReviewedRevenue INTEGER NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Shifts ADD COLUMN ReviewedCash INTEGER NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Shifts ADD COLUMN ReviewedKnet INTEGER NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Shifts ADD COLUMN ReviewedExpenses INTEGER NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Shifts ADD COLUMN ReviewedWithdrawals INTEGER NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Shifts ADD COLUMN ReviewedDeposits INTEGER NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Shifts ADD COLUMN ReviewedAdvances INTEGER NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Shifts ADD COLUMN ReviewedEmployeeDebts INTEGER NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Shifts ADD COLUMN ReviewedCustody INTEGER NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Shifts ADD COLUMN ConfirmedNoDiscrepancies INTEGER NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Shifts ADD COLUMN ApprovalNotes TEXT NULL");
            TryExec("ALTER TABLE Shifts ADD COLUMN ApprovedByUserId TEXT NULL");
            TryExec("ALTER TABLE Shifts ADD COLUMN ApprovedByUserName TEXT NULL");
            TryExec("ALTER TABLE Shifts ADD COLUMN ApprovedAt TEXT NULL");
            TryExec("ALTER TABLE Shifts ADD COLUMN IsAutoClosed INTEGER NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Shifts ADD COLUMN AutoClosedAt TEXT NULL");
            TryExec("ALTER TABLE Shifts ADD COLUMN ReopenedByUserId TEXT NULL");
            TryExec("ALTER TABLE Shifts ADD COLUMN ReopenedByUserName TEXT NULL");
            TryExec("ALTER TABLE Shifts ADD COLUMN ReopenedAt TEXT NULL");
            TryExec("ALTER TABLE Shifts ADD COLUMN ReopenReason TEXT NULL");
            // اليومية "المغلقة" حالياً (النظام القديم) تُعتبر معتمدة تلقائياً حتى لا تُقفل كل
            // بيانات الماضي فجأة بميزة القفل الجديدة.
            TryExec("UPDATE Shifts SET ApprovalStatus = 'معتمد' WHERE Status = 'مغلق' AND ApprovalStatus = 'مفتوح'");
            // يفصل سجلات اعتماد الإغلاق عن سجلات "الشفتات" العادية حتى ما تبقاش الشاشتين شغالين
            // على نفس الصف (انظر التعليق فوق تعريف IsClosureRecord في Shift.cs).
            TryExec("ALTER TABLE Shifts ADD COLUMN IsClosureRecord INTEGER NOT NULL DEFAULT 0");
            // يفصل يومية كل قسم (حلاقة/مساج) عن التانية، والبنود المشتركة بدون قسم في نطاق "عام"
            // (انظر التعليق فوق تعريف ClosureDepartments في Shift.cs).
            TryExec("ALTER TABLE Shifts ADD COLUMN ClosureDepartment TEXT NULL");

            // شراء آجل من المورد — ذمة مستحقة بدل خصم فوري من العهدة/الصندوق/البنك (انظر
            // PurchaseRequest.PurchaseMethods وSupplierInvoice)
            TryExec("ALTER TABLE PurchaseRequests ADD COLUMN PurchaseMethod TEXT NOT NULL DEFAULT 'نقدًا من العهدة'");
            TryExec("ALTER TABLE PurchaseRequests ADD COLUMN SupplierInvoiceId INTEGER NULL");
            TryExec(@"CREATE TABLE IF NOT EXISTS SupplierInvoices (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SupplierId INTEGER NOT NULL,
                PurchaseRequestId INTEGER NULL,
                InvoiceNumber TEXT NOT NULL,
                InvoiceDate TEXT NOT NULL DEFAULT (date('now')),
                TotalAmount REAL NOT NULL DEFAULT 0,
                Notes TEXT NULL,
                CreatedByUserId TEXT NULL,
                CreatedByName TEXT NULL,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (SupplierId) REFERENCES Suppliers(Id),
                FOREIGN KEY (PurchaseRequestId) REFERENCES PurchaseRequests(Id))");
            TryExec(@"CREATE TABLE IF NOT EXISTS SupplierInvoiceInstallments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SupplierInvoiceId INTEGER NOT NULL,
                SequenceNo INTEGER NOT NULL DEFAULT 1,
                Amount REAL NOT NULL DEFAULT 0,
                DueDate TEXT NOT NULL DEFAULT (date('now')),
                FOREIGN KEY (SupplierInvoiceId) REFERENCES SupplierInvoices(Id) ON DELETE CASCADE)");
            TryExec(@"CREATE TABLE IF NOT EXISTS SupplierInvoicePayments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SupplierInvoiceId INTEGER NOT NULL,
                Amount REAL NOT NULL DEFAULT 0,
                PaymentDate TEXT NOT NULL DEFAULT (date('now')),
                Source TEXT NOT NULL DEFAULT 'الصندوق',
                CustodyId INTEGER NULL,
                ReferenceNumber TEXT NULL,
                Notes TEXT NULL,
                ExpenseId INTEGER NULL,
                PaidByUserId TEXT NULL,
                PaidByName TEXT NULL,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (SupplierInvoiceId) REFERENCES SupplierInvoices(Id) ON DELETE CASCADE,
                FOREIGN KEY (CustodyId) REFERENCES Custodies(Id),
                FOREIGN KEY (ExpenseId) REFERENCES Expenses(Id))");

            // تطوير فواتير الموردين الآجلة: أسطر منتجات مرتبطة بالمخزون + مرفقات + خصم/مصاريف
            // إضافية + مسودة/إلغاء + مرجع داخلي تلقائي (انظر SupplierInvoice وSupplierInvoiceItem)
            TryExec("ALTER TABLE SupplierInvoices ADD COLUMN Reference TEXT NULL");
            TryExec("ALTER TABLE SupplierInvoices ADD COLUMN DiscountAmount REAL NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE SupplierInvoices ADD COLUMN ExtraExpenses REAL NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE SupplierInvoices ADD COLUMN AttachmentPath TEXT NULL");
            TryExec("ALTER TABLE SupplierInvoices ADD COLUMN IsDraft INTEGER NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE SupplierInvoices ADD COLUMN IsCancelled INTEGER NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE SupplierInvoices ADD COLUMN CancelledAt TEXT NULL");
            TryExec("ALTER TABLE SupplierInvoices ADD COLUMN CancelledByName TEXT NULL");
            TryExec("ALTER TABLE SupplierInvoices ADD COLUMN CancelReason TEXT NULL");
            TryExec("ALTER TABLE SupplierInvoices ADD COLUMN ReturnReason TEXT NULL");
            TryExec("ALTER TABLE SupplierInvoices ADD COLUMN ReturnedAt TEXT NULL");
            TryExec("ALTER TABLE SupplierInvoices ADD COLUMN ReturnedByName TEXT NULL");
            TryExec("ALTER TABLE SupplierInvoices ADD COLUMN Department TEXT NULL");
            TryExec("ALTER TABLE SupplierInvoicePayments ADD COLUMN AttachmentPath TEXT NULL");
            TryExec(@"CREATE TABLE IF NOT EXISTS SupplierInvoiceItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SupplierInvoiceId INTEGER NOT NULL,
                ProductId INTEGER NULL,
                ProductName TEXT NOT NULL,
                Unit TEXT NULL,
                Quantity INTEGER NOT NULL DEFAULT 1,
                UnitPrice REAL NOT NULL DEFAULT 0,
                Discount REAL NOT NULL DEFAULT 0,
                FOREIGN KEY (SupplierInvoiceId) REFERENCES SupplierInvoices(Id) ON DELETE CASCADE,
                FOREIGN KEY (ProductId) REFERENCES Products(Id))");
        }
        else
        {
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='AmountPaid') ALTER TABLE EmployeeAdvances ADD AmountPaid DECIMAL(18,3) NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='ManagerId') ALTER TABLE EmployeeAdvances ADD ManagerId NVARCHAR(450) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='ManagerName') ALTER TABLE EmployeeAdvances ADD ManagerName NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='DecisionAt') ALTER TABLE EmployeeAdvances ADD DecisionAt DATETIME NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='RejectionReason') ALTER TABLE EmployeeAdvances ADD RejectionReason NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='CashierId') ALTER TABLE EmployeeAdvances ADD CashierId NVARCHAR(450) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='CashierName') ALTER TABLE EmployeeAdvances ADD CashierName NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='DisbursedAt') ALTER TABLE EmployeeAdvances ADD DisbursedAt DATETIME NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='TransferReference') ALTER TABLE EmployeeAdvances ADD TransferReference NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='TransferBank') ALTER TABLE EmployeeAdvances ADD TransferBank NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='TransferNotes') ALTER TABLE EmployeeAdvances ADD TransferNotes NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='TransferredById') ALTER TABLE EmployeeAdvances ADD TransferredById NVARCHAR(450) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='TransferredByName') ALTER TABLE EmployeeAdvances ADD TransferredByName NVARCHAR(MAX) NULL");
            TryExec("UPDATE EmployeeAdvances SET Status = N'بانتظار موافقة المدير' WHERE Status = N'معلق'");
            TryExec("UPDATE EmployeeAdvances SET Status = N'تم الصرف' WHERE Status = N'موافق عليها' AND PaymentMethod = N'نقدي'");
            TryExec("UPDATE EmployeeAdvances SET Status = N'تم التحويل' WHERE Status = N'موافق عليها' AND PaymentMethod <> N'نقدي'");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Products' AND COLUMN_NAME='SupplierId') ALTER TABLE Products ADD SupplierId INT NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Products' AND COLUMN_NAME='OpeningQuantity') ALTER TABLE Products ADD OpeningQuantity INT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Attendances' AND COLUMN_NAME='QueuePosition') ALTER TABLE Attendances ADD QueuePosition INT NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Sales' AND COLUMN_NAME='KnetReceiptNumber') ALTER TABLE Sales ADD KnetReceiptNumber NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Sales' AND COLUMN_NAME='EmployeeGift') ALTER TABLE Sales ADD EmployeeGift DECIMAL(18,3) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Sales' AND COLUMN_NAME='GiftForEmployee') ALTER TABLE Sales ADD GiftForEmployee DECIMAL(18,3) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Sales' AND COLUMN_NAME='PaidAmount') ALTER TABLE Sales ADD PaidAmount DECIMAL(18,3) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Sales' AND COLUMN_NAME='ChangeAmount') ALTER TABLE Sales ADD ChangeAmount DECIMAL(18,3) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Sales' AND COLUMN_NAME='CreatedByUserId') ALTER TABLE Sales ADD CreatedByUserId NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Sales' AND COLUMN_NAME='CreatedByUserName') ALTER TABLE Sales ADD CreatedByUserName NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Salaries' AND COLUMN_NAME='GiftAmount') ALTER TABLE Salaries ADD GiftAmount DECIMAL(18,3) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Salaries' AND COLUMN_NAME='HadiyaAmount') ALTER TABLE Salaries ADD HadiyaAmount DECIMAL(18,3) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Salaries' AND COLUMN_NAME='CommissionAmount') ALTER TABLE Salaries ADD CommissionAmount DECIMAL(18,3) NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Salaries' AND COLUMN_NAME='PaymentMethod') ALTER TABLE Salaries ADD PaymentMethod NVARCHAR(50) NOT NULL DEFAULT N'����'");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Salaries' AND COLUMN_NAME='EmployeeDebtDeducted') ALTER TABLE Salaries ADD EmployeeDebtDeducted DECIMAL(18,3) NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Employees' AND COLUMN_NAME='SalesTarget') ALTER TABLE Employees ADD SalesTarget DECIMAL(18,3) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Employees' AND COLUMN_NAME='CommissionAfterTarget') ALTER TABLE Employees ADD CommissionAfterTarget DECIMAL(18,2) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Employees' AND COLUMN_NAME='RevenueDepartment') ALTER TABLE Employees ADD RevenueDepartment NVARCHAR(MAX) NULL");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Suppliers')
                CREATE TABLE Suppliers (Id INT IDENTITY PRIMARY KEY, Name NVARCHAR(200) NOT NULL,
                Phone NVARCHAR(50), Email NVARCHAR(200), Address NVARCHAR(500), Notes NVARCHAR(MAX),
                IsActive BIT NOT NULL DEFAULT 1, CreatedAt DATETIME NOT NULL DEFAULT GETDATE())");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='StockMovements')
                CREATE TABLE StockMovements (Id INT IDENTITY PRIMARY KEY, ProductId INT NOT NULL,
                MovementType NVARCHAR(50) NOT NULL DEFAULT N'??????', Quantity INT NOT NULL,
                UnitPrice DECIMAL(18,3) NOT NULL DEFAULT 0, EmployeeId INT NULL, SupplierId INT NULL,
                Notes NVARCHAR(MAX), MovementDate DATE NOT NULL DEFAULT GETDATE(), CreatedAt DATETIME NOT NULL DEFAULT GETDATE())");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='AttendancePermissions')
                CREATE TABLE AttendancePermissions (Id INT IDENTITY PRIMARY KEY,
                AttendanceId INT NOT NULL, LeaveTime TIME NOT NULL, ReturnTime TIME NULL,
                Notes NVARCHAR(MAX), CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_AttendancePermissions_Attendances FOREIGN KEY (AttendanceId)
                    REFERENCES Attendances(Id) ON DELETE CASCADE)");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ServicePackages')
                CREATE TABLE ServicePackages (Id INT IDENTITY PRIMARY KEY,
                NameAr NVARCHAR(200) NOT NULL, NameEn NVARCHAR(200) NULL,
                SessionCount INT NOT NULL DEFAULT 4, Price DECIMAL(18,3) NOT NULL DEFAULT 0,
                ValidityDays INT NOT NULL DEFAULT 90, Description NVARCHAR(MAX),
                IsActive BIT NOT NULL DEFAULT 1, CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                ServiceCategoryId INT NULL,
                CONSTRAINT FK_ServicePackages_ServiceCategories FOREIGN KEY (ServiceCategoryId)
                    REFERENCES ServiceCategories(Id) ON DELETE SET NULL)");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='CustomerPackages')
                CREATE TABLE CustomerPackages (Id INT IDENTITY PRIMARY KEY,
                CustomerId INT NOT NULL, ServicePackageId INT NOT NULL,
                PurchaseDate DATE NOT NULL DEFAULT GETDATE(), ExpiryDate DATE NULL,
                TotalSessions INT NOT NULL DEFAULT 0, RemainingSessions INT NOT NULL DEFAULT 0,
                PricePaid DECIMAL(18,3) NOT NULL DEFAULT 0, Notes NVARCHAR(MAX),
                IsActive BIT NOT NULL DEFAULT 1, CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_CustomerPackages_Customers FOREIGN KEY (CustomerId)
                    REFERENCES Customers(Id),
                CONSTRAINT FK_CustomerPackages_ServicePackages FOREIGN KEY (ServicePackageId)
                    REFERENCES ServicePackages(Id))");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Deposits')
                CREATE TABLE Deposits (Id INT IDENTITY PRIMARY KEY,
                Amount DECIMAL(18,3) NOT NULL DEFAULT 0,
                Description NVARCHAR(MAX) NOT NULL,
                Source NVARCHAR(MAX) NULL,
                DepositDate DATE NOT NULL DEFAULT GETDATE(),
                Notes NVARCHAR(MAX) NULL,
                Department NVARCHAR(MAX) NOT NULL DEFAULT N'حلاقة',
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE())");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Deposits' AND COLUMN_NAME='Department') ALTER TABLE Deposits ADD Department NVARCHAR(MAX) NOT NULL DEFAULT N'حلاقة'");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Deposits' AND COLUMN_NAME='PaymentMethod') ALTER TABLE Deposits ADD PaymentMethod NVARCHAR(50) NOT NULL DEFAULT N'نقدي'");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Appointments' AND COLUMN_NAME='EndTime') ALTER TABLE Appointments ADD EndTime TIME NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Appointments' AND COLUMN_NAME='CustomerPackageId') ALTER TABLE Appointments ADD CustomerPackageId INT NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Customers' AND COLUMN_NAME='AssignedEmployeeId') ALTER TABLE Customers ADD AssignedEmployeeId INT NULL");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Withdrawals')
                CREATE TABLE Withdrawals (Id INT IDENTITY PRIMARY KEY,
                Amount DECIMAL(18,3) NOT NULL DEFAULT 0,
                Description NVARCHAR(MAX) NOT NULL,
                Reason NVARCHAR(MAX) NULL,
                WithdrawalDate DATE NOT NULL DEFAULT GETDATE(),
                Notes NVARCHAR(MAX) NULL,
                Department NVARCHAR(100) NULL,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE())");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Withdrawals' AND COLUMN_NAME='Department') ALTER TABLE Withdrawals ADD Department NVARCHAR(100) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Withdrawals' AND COLUMN_NAME='PaymentMethod') ALTER TABLE Withdrawals ADD PaymentMethod NVARCHAR(50) NOT NULL DEFAULT N'نقدي'");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='AppSettings')
                CREATE TABLE AppSettings ([Key] NVARCHAR(100) PRIMARY KEY, Value NVARCHAR(MAX) NOT NULL DEFAULT N'')");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='CustomerPackageTransactions')
                CREATE TABLE CustomerPackageTransactions (Id INT IDENTITY PRIMARY KEY,
                CustomerPackageId INT NOT NULL, UsedDate DATETIME NOT NULL DEFAULT GETDATE(),
                EmployeeId INT NULL, Notes NVARCHAR(MAX),
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_CustPkgTrans_CustomerPackages FOREIGN KEY (CustomerPackageId)
                    REFERENCES CustomerPackages(Id) ON DELETE CASCADE,
                CONSTRAINT FK_CustPkgTrans_Employees FOREIGN KEY (EmployeeId)
                    REFERENCES Employees(Id) ON DELETE SET NULL)");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='AppointmentReminders')
                CREATE TABLE AppointmentReminders (Id INT IDENTITY PRIMARY KEY,
                AppointmentId INT NOT NULL, MinutesBefore INT NOT NULL DEFAULT 60,
                IsSent BIT NOT NULL DEFAULT 0, SentAt DATETIME NULL,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_ApptReminder_Appointments FOREIGN KEY (AppointmentId)
                    REFERENCES Appointments(Id) ON DELETE CASCADE)");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Custodies')
                CREATE TABLE Custodies (Id INT IDENTITY PRIMARY KEY,
                EmployeeId INT NOT NULL,
                Amount DECIMAL(18,3) NOT NULL DEFAULT 0,
                CustodyDate DATE NOT NULL DEFAULT GETDATE(),
                PaymentMethod NVARCHAR(50) NOT NULL DEFAULT N'نقدي',
                Reason NVARCHAR(MAX) NULL,
                Notes NVARCHAR(MAX) NULL,
                ExpenseId INT NULL,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_Custodies_Employees FOREIGN KEY (EmployeeId)
                    REFERENCES Employees(Id),
                CONSTRAINT FK_Custodies_Expenses FOREIGN KEY (ExpenseId)
                    REFERENCES Expenses(Id))");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Custodies' AND COLUMN_NAME='ExpenseId') ALTER TABLE Custodies ADD ExpenseId INT NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Expenses' AND COLUMN_NAME='EmployeeId') ALTER TABLE Expenses ADD EmployeeId INT NULL");
            // نظام تسوية/إرجاع العهد القديم أُلغي بالكامل واستُبدل بتدفق طلبات الشراء أدناه
            TryExec("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='CustodySettlements') DROP TABLE CustodySettlements");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='PurchaseRequests')
                CREATE TABLE PurchaseRequests (Id INT IDENTITY PRIMARY KEY,
                EmployeeId INT NOT NULL,
                CustodyId INT NOT NULL DEFAULT 0,
                RequestDate DATE NOT NULL DEFAULT GETDATE(),
                Reason NVARCHAR(MAX) NOT NULL DEFAULT N'',
                SupplierId INT NULL,
                EstimatedAmount DECIMAL(18,3) NOT NULL DEFAULT 0,
                Notes NVARCHAR(MAX) NULL,
                Status NVARCHAR(50) NOT NULL DEFAULT N'بانتظار موافقة المدير',
                RejectionReason NVARCHAR(MAX) NULL,
                ApprovedByUserId NVARCHAR(450) NULL,
                ApprovedByName NVARCHAR(MAX) NULL,
                ApprovedAt DATETIME NULL,
                InvoiceNumber NVARCHAR(200) NULL,
                ActualAmount DECIMAL(18,3) NULL,
                InvoicePhotoPath NVARCHAR(500) NULL,
                ReviewedByUserId NVARCHAR(450) NULL,
                ReviewedByName NVARCHAR(MAX) NULL,
                ReviewedAt DATETIME NULL,
                ExpenseId INT NULL,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_PurchaseRequests_Employees FOREIGN KEY (EmployeeId)
                    REFERENCES Employees(Id),
                CONSTRAINT FK_PurchaseRequests_Custodies FOREIGN KEY (CustodyId)
                    REFERENCES Custodies(Id),
                CONSTRAINT FK_PurchaseRequests_Suppliers FOREIGN KEY (SupplierId)
                    REFERENCES Suppliers(Id),
                CONSTRAINT FK_PurchaseRequests_Expenses FOREIGN KEY (ExpenseId)
                    REFERENCES Expenses(Id))");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PurchaseRequests' AND COLUMN_NAME='CustodyId') ALTER TABLE PurchaseRequests ADD CustodyId INT NOT NULL DEFAULT 0");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='PurchaseRequestItems')
                CREATE TABLE PurchaseRequestItems (Id INT IDENTITY PRIMARY KEY,
                PurchaseRequestId INT NOT NULL,
                ProductName NVARCHAR(300) NOT NULL,
                Quantity INT NOT NULL DEFAULT 1,
                CONSTRAINT FK_PurchaseRequestItems_PurchaseRequests FOREIGN KEY (PurchaseRequestId)
                    REFERENCES PurchaseRequests(Id) ON DELETE CASCADE)");
            // يمنع تكرار رقم الفاتورة عند طلبين متزامنين (double-click / إعادة إرسال) — لو فيه
            // تكرار موجود بالفعل في البيانات هيفشل بصمت (TryExec) لحد ما يتصلّح يدويًا
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Sales_InvoiceNumber' AND object_id = OBJECT_ID('Sales'))
                CREATE UNIQUE INDEX IX_Sales_InvoiceNumber ON Sales(InvoiceNumber) WHERE InvoiceNumber IS NOT NULL AND InvoiceNumber <> ''");

            // نظام اعتماد وإغلاق اليومية
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ApprovalStatus') ALTER TABLE Shifts ADD ApprovalStatus NVARCHAR(100) NOT NULL DEFAULT N'مفتوح'");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ExpectedCashBalance') ALTER TABLE Shifts ADD ExpectedCashBalance DECIMAL(18,3) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='CashDifferenceReason') ALTER TABLE Shifts ADD CashDifferenceReason NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='SystemKnetTotal') ALTER TABLE Shifts ADD SystemKnetTotal DECIMAL(18,3) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='DeviceKnetTotal') ALTER TABLE Shifts ADD DeviceKnetTotal DECIMAL(18,3) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='KnetSettlementNumber') ALTER TABLE Shifts ADD KnetSettlementNumber NVARCHAR(200) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='KnetDifferenceReason') ALTER TABLE Shifts ADD KnetDifferenceReason NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ReviewedRevenue') ALTER TABLE Shifts ADD ReviewedRevenue BIT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ReviewedCash') ALTER TABLE Shifts ADD ReviewedCash BIT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ReviewedKnet') ALTER TABLE Shifts ADD ReviewedKnet BIT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ReviewedExpenses') ALTER TABLE Shifts ADD ReviewedExpenses BIT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ReviewedWithdrawals') ALTER TABLE Shifts ADD ReviewedWithdrawals BIT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ReviewedDeposits') ALTER TABLE Shifts ADD ReviewedDeposits BIT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ReviewedAdvances') ALTER TABLE Shifts ADD ReviewedAdvances BIT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ReviewedEmployeeDebts') ALTER TABLE Shifts ADD ReviewedEmployeeDebts BIT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ReviewedCustody') ALTER TABLE Shifts ADD ReviewedCustody BIT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ConfirmedNoDiscrepancies') ALTER TABLE Shifts ADD ConfirmedNoDiscrepancies BIT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ApprovalNotes') ALTER TABLE Shifts ADD ApprovalNotes NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ApprovedByUserId') ALTER TABLE Shifts ADD ApprovedByUserId NVARCHAR(450) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ApprovedByUserName') ALTER TABLE Shifts ADD ApprovedByUserName NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ApprovedAt') ALTER TABLE Shifts ADD ApprovedAt DATETIME NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='IsAutoClosed') ALTER TABLE Shifts ADD IsAutoClosed BIT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='AutoClosedAt') ALTER TABLE Shifts ADD AutoClosedAt DATETIME NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ReopenedByUserId') ALTER TABLE Shifts ADD ReopenedByUserId NVARCHAR(450) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ReopenedByUserName') ALTER TABLE Shifts ADD ReopenedByUserName NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ReopenedAt') ALTER TABLE Shifts ADD ReopenedAt DATETIME NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ReopenReason') ALTER TABLE Shifts ADD ReopenReason NVARCHAR(MAX) NULL");
            // اليومية "المغلقة" حالياً (النظام القديم) تُعتبر معتمدة تلقائياً حتى لا تُقفل كل
            // بيانات الماضي فجأة بميزة القفل الجديدة.
            TryExec("UPDATE Shifts SET ApprovalStatus = N'معتمد' WHERE Status = N'مغلق' AND ApprovalStatus = N'مفتوح'");
            // يفصل سجلات اعتماد الإغلاق عن سجلات "الشفتات" العادية حتى ما تبقاش الشاشتين شغالين
            // على نفس الصف (انظر التعليق فوق تعريف IsClosureRecord في Shift.cs).
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='IsClosureRecord') ALTER TABLE Shifts ADD IsClosureRecord BIT NOT NULL DEFAULT 0");
            // يفصل يومية كل قسم (حلاقة/مساج) عن التانية، والبنود المشتركة بدون قسم في نطاق "عام"
            // (انظر التعليق فوق تعريف ClosureDepartments في Shift.cs).
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Shifts' AND COLUMN_NAME='ClosureDepartment') ALTER TABLE Shifts ADD ClosureDepartment NVARCHAR(50) NULL");

            // شراء آجل من المورد — ذمة مستحقة بدل خصم فوري من العهدة/الصندوق/البنك (انظر
            // PurchaseRequest.PurchaseMethods وSupplierInvoice)
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PurchaseRequests' AND COLUMN_NAME='PurchaseMethod') ALTER TABLE PurchaseRequests ADD PurchaseMethod NVARCHAR(100) NOT NULL DEFAULT N'نقدًا من العهدة'");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PurchaseRequests' AND COLUMN_NAME='SupplierInvoiceId') ALTER TABLE PurchaseRequests ADD SupplierInvoiceId INT NULL");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='SupplierInvoices')
                CREATE TABLE SupplierInvoices (Id INT IDENTITY PRIMARY KEY,
                SupplierId INT NOT NULL,
                PurchaseRequestId INT NULL,
                InvoiceNumber NVARCHAR(200) NOT NULL,
                InvoiceDate DATE NOT NULL DEFAULT GETDATE(),
                TotalAmount DECIMAL(18,3) NOT NULL DEFAULT 0,
                Notes NVARCHAR(MAX) NULL,
                CreatedByUserId NVARCHAR(450) NULL,
                CreatedByName NVARCHAR(MAX) NULL,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_SupplierInvoices_Suppliers FOREIGN KEY (SupplierId)
                    REFERENCES Suppliers(Id),
                CONSTRAINT FK_SupplierInvoices_PurchaseRequests FOREIGN KEY (PurchaseRequestId)
                    REFERENCES PurchaseRequests(Id))");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='SupplierInvoiceInstallments')
                CREATE TABLE SupplierInvoiceInstallments (Id INT IDENTITY PRIMARY KEY,
                SupplierInvoiceId INT NOT NULL,
                SequenceNo INT NOT NULL DEFAULT 1,
                Amount DECIMAL(18,3) NOT NULL DEFAULT 0,
                DueDate DATE NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_SupplierInvoiceInstallments_SupplierInvoices FOREIGN KEY (SupplierInvoiceId)
                    REFERENCES SupplierInvoices(Id) ON DELETE CASCADE)");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='SupplierInvoicePayments')
                CREATE TABLE SupplierInvoicePayments (Id INT IDENTITY PRIMARY KEY,
                SupplierInvoiceId INT NOT NULL,
                Amount DECIMAL(18,3) NOT NULL DEFAULT 0,
                PaymentDate DATE NOT NULL DEFAULT GETDATE(),
                Source NVARCHAR(50) NOT NULL DEFAULT N'الصندوق',
                CustodyId INT NULL,
                ReferenceNumber NVARCHAR(200) NULL,
                Notes NVARCHAR(MAX) NULL,
                ExpenseId INT NULL,
                PaidByUserId NVARCHAR(450) NULL,
                PaidByName NVARCHAR(MAX) NULL,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_SupplierInvoicePayments_SupplierInvoices FOREIGN KEY (SupplierInvoiceId)
                    REFERENCES SupplierInvoices(Id) ON DELETE CASCADE,
                CONSTRAINT FK_SupplierInvoicePayments_Custodies FOREIGN KEY (CustodyId)
                    REFERENCES Custodies(Id),
                CONSTRAINT FK_SupplierInvoicePayments_Expenses FOREIGN KEY (ExpenseId)
                    REFERENCES Expenses(Id))");

            // تطوير فواتير الموردين الآجلة: أسطر منتجات مرتبطة بالمخزون + مرفقات + خصم/مصاريف
            // إضافية + مسودة/إلغاء + مرجع داخلي تلقائي (انظر SupplierInvoice وSupplierInvoiceItem)
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupplierInvoices' AND COLUMN_NAME='Reference') ALTER TABLE SupplierInvoices ADD Reference NVARCHAR(50) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupplierInvoices' AND COLUMN_NAME='DiscountAmount') ALTER TABLE SupplierInvoices ADD DiscountAmount DECIMAL(18,3) NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupplierInvoices' AND COLUMN_NAME='ExtraExpenses') ALTER TABLE SupplierInvoices ADD ExtraExpenses DECIMAL(18,3) NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupplierInvoices' AND COLUMN_NAME='AttachmentPath') ALTER TABLE SupplierInvoices ADD AttachmentPath NVARCHAR(500) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupplierInvoices' AND COLUMN_NAME='IsDraft') ALTER TABLE SupplierInvoices ADD IsDraft BIT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupplierInvoices' AND COLUMN_NAME='IsCancelled') ALTER TABLE SupplierInvoices ADD IsCancelled BIT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupplierInvoices' AND COLUMN_NAME='CancelledAt') ALTER TABLE SupplierInvoices ADD CancelledAt DATETIME NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupplierInvoices' AND COLUMN_NAME='CancelledByName') ALTER TABLE SupplierInvoices ADD CancelledByName NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupplierInvoices' AND COLUMN_NAME='CancelReason') ALTER TABLE SupplierInvoices ADD CancelReason NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupplierInvoices' AND COLUMN_NAME='ReturnReason') ALTER TABLE SupplierInvoices ADD ReturnReason NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupplierInvoices' AND COLUMN_NAME='ReturnedAt') ALTER TABLE SupplierInvoices ADD ReturnedAt DATETIME NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupplierInvoices' AND COLUMN_NAME='ReturnedByName') ALTER TABLE SupplierInvoices ADD ReturnedByName NVARCHAR(MAX) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupplierInvoices' AND COLUMN_NAME='Department') ALTER TABLE SupplierInvoices ADD Department NVARCHAR(50) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupplierInvoicePayments' AND COLUMN_NAME='AttachmentPath') ALTER TABLE SupplierInvoicePayments ADD AttachmentPath NVARCHAR(500) NULL");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='SupplierInvoiceItems')
                CREATE TABLE SupplierInvoiceItems (Id INT IDENTITY PRIMARY KEY,
                SupplierInvoiceId INT NOT NULL,
                ProductId INT NULL,
                ProductName NVARCHAR(300) NOT NULL,
                Unit NVARCHAR(50) NULL,
                Quantity INT NOT NULL DEFAULT 1,
                UnitPrice DECIMAL(18,3) NOT NULL DEFAULT 0,
                Discount DECIMAL(18,3) NOT NULL DEFAULT 0,
                CONSTRAINT FK_SupplierInvoiceItems_SupplierInvoices FOREIGN KEY (SupplierInvoiceId)
                    REFERENCES SupplierInvoices(Id) ON DELETE CASCADE,
                CONSTRAINT FK_SupplierInvoiceItems_Products FOREIGN KEY (ProductId)
                    REFERENCES Products(Id))");
        }

        // ترحيل لمرة واحدة: قبل هذا الفصل كانت شاشة "اعتماد اليومية" تعيد استخدام آخر صف Shift
        // موجود لليوم (أياً كان مصدره) كأساس الإغلاق. بنحدد نفس الصف اللي كان النظام القديم
        // شغال عليه (آخر صف بتاريخ الإنشاء لكل يوم) ونعلّمه IsClosureRecord = true حتى ما تضيعش
        // حالة أي يومية سابقة. الشرط "لو مفيش أي صف متعلَّم أصلاً" يخلّي هذا الكود ميتكررش على
        // بيانات جديدة بعد التفعيل (كل يومية جديدة بتتعلّم من كود DailyClosureService مباشرة).
        try
        {
            bool anyClosureMarked = await context.Shifts.AnyAsync(s => s.IsClosureRecord);
            if (!anyClosureMarked)
            {
                var legacyShifts = await context.Shifts.ToListAsync();
                var legacyClosureRows = legacyShifts
                    .GroupBy(s => s.ShiftDate.Date)
                    .Select(g => g.OrderByDescending(s => s.CreatedAt).First());
                foreach (var s in legacyClosureRows) s.IsClosureRecord = true;
                await context.SaveChangesAsync();
            }
        }
        catch { }

        // ترحيل لمرة واحدة (منفصل عن اللي فوق): قبل تقسيم اليومية على الأقسام، كل يومية كانت
        // تمثّل المحل بالكامل مجمّعاً، فبنحطها في نطاق "عام" بدل ما تتحذف أو تفضل بدون قسم.
        // كل يومية جديدة بعد كده بتتحدد قسمها (حلاقة/مساج/عام) وقت الإنشاء من DailyClosureService.
        try
        {
            bool anyDepartmentMarked = await context.Shifts.AnyAsync(s => s.IsClosureRecord && s.ClosureDepartment != null);
            if (!anyDepartmentMarked)
            {
                var unassigned = await context.Shifts
                    .Where(s => s.IsClosureRecord && s.ClosureDepartment == null)
                    .ToListAsync();
                foreach (var s in unassigned) s.ClosureDepartment = Shift.ClosureDepartments.Shared;
                await context.SaveChangesAsync();
            }
        }
        catch { }

        await SeedData.InitializeAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the database.");
    }
}

app.Run();