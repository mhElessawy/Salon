using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;
using Salon.Services;

var builder = WebApplication.CreateBuilder(args);

// Add EF Core - Use SQLite on Linux, SQL Server on Windows
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
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

// Email service
var emailSettings = builder.Configuration.GetSection("EmailSettings").Get<Salon.Services.EmailSettings>()
    ?? new Salon.Services.EmailSettings();
builder.Services.AddSingleton(emailSettings);
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<Salon.Services.ReminderWorker>();

builder.Services.AddMemoryCache();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
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
            TryExec(@"CREATE TABLE IF NOT EXISTS CustodySettlements (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CustodyId INTEGER NOT NULL,
                Amount REAL NOT NULL DEFAULT 0,
                SettlementDate TEXT NOT NULL DEFAULT (date('now')),
                PaymentMethod TEXT NOT NULL DEFAULT 'نقدي',
                Notes TEXT,
                Status TEXT NOT NULL DEFAULT 'معلق',
                RejectionReason TEXT,
                DepositId INTEGER NULL,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (CustodyId) REFERENCES Custodies(Id) ON DELETE CASCADE,
                FOREIGN KEY (DepositId) REFERENCES Deposits(Id))");
            TryExec("ALTER TABLE CustodySettlements ADD COLUMN Status TEXT NOT NULL DEFAULT 'معلق'");
            TryExec("ALTER TABLE CustodySettlements ADD COLUMN RejectionReason TEXT");
        }
        else
        {
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='AmountPaid') ALTER TABLE EmployeeAdvances ADD AmountPaid DECIMAL(18,3) NOT NULL DEFAULT 0");
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
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='CustodySettlements')
                CREATE TABLE CustodySettlements (Id INT IDENTITY PRIMARY KEY,
                CustodyId INT NOT NULL,
                Amount DECIMAL(18,3) NOT NULL DEFAULT 0,
                SettlementDate DATE NOT NULL DEFAULT GETDATE(),
                PaymentMethod NVARCHAR(50) NOT NULL DEFAULT N'نقدي',
                Notes NVARCHAR(MAX) NULL,
                Status NVARCHAR(50) NOT NULL DEFAULT N'معلق',
                RejectionReason NVARCHAR(MAX) NULL,
                DepositId INT NULL,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_CustodySettlements_Custodies FOREIGN KEY (CustodyId)
                    REFERENCES Custodies(Id) ON DELETE CASCADE,
                CONSTRAINT FK_CustodySettlements_Deposits FOREIGN KEY (DepositId)
                    REFERENCES Deposits(Id))");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='CustodySettlements' AND COLUMN_NAME='Status') ALTER TABLE CustodySettlements ADD Status NVARCHAR(50) NOT NULL DEFAULT N'معلق'");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='CustodySettlements' AND COLUMN_NAME='RejectionReason') ALTER TABLE CustodySettlements ADD RejectionReason NVARCHAR(MAX) NULL");
        }

        await SeedData.InitializeAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the database.");
    }
}

app.Run();