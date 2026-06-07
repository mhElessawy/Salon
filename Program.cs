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
            TryExec("ALTER TABLE Sales ADD COLUMN EmployeeGift REAL NULL");
            TryExec("ALTER TABLE Sales ADD COLUMN GiftForEmployee REAL NULL");
            TryExec("ALTER TABLE Salaries ADD COLUMN GiftAmount REAL NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Salaries ADD COLUMN HadiyaAmount REAL NULL");
            TryExec("ALTER TABLE Salaries ADD COLUMN CommissionAmount REAL NOT NULL DEFAULT 0");
            TryExec("ALTER TABLE Salaries ADD COLUMN PaymentMethod TEXT NOT NULL DEFAULT '����'");
            TryExec("ALTER TABLE Salaries ADD COLUMN EmployeeDebtDeducted REAL NOT NULL DEFAULT 0");
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
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')))");
        }
        else
        {
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='AmountPaid') ALTER TABLE EmployeeAdvances ADD AmountPaid DECIMAL(18,3) NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Products' AND COLUMN_NAME='SupplierId') ALTER TABLE Products ADD SupplierId INT NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Products' AND COLUMN_NAME='OpeningQuantity') ALTER TABLE Products ADD OpeningQuantity INT NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Attendances' AND COLUMN_NAME='QueuePosition') ALTER TABLE Attendances ADD QueuePosition INT NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Sales' AND COLUMN_NAME='EmployeeGift') ALTER TABLE Sales ADD EmployeeGift DECIMAL(18,3) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Sales' AND COLUMN_NAME='GiftForEmployee') ALTER TABLE Sales ADD GiftForEmployee DECIMAL(18,3) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Salaries' AND COLUMN_NAME='GiftAmount') ALTER TABLE Salaries ADD GiftAmount DECIMAL(18,3) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Salaries' AND COLUMN_NAME='HadiyaAmount') ALTER TABLE Salaries ADD HadiyaAmount DECIMAL(18,3) NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Salaries' AND COLUMN_NAME='CommissionAmount') ALTER TABLE Salaries ADD CommissionAmount DECIMAL(18,3) NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Salaries' AND COLUMN_NAME='PaymentMethod') ALTER TABLE Salaries ADD PaymentMethod NVARCHAR(50) NOT NULL DEFAULT N'����'");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Salaries' AND COLUMN_NAME='EmployeeDebtDeducted') ALTER TABLE Salaries ADD EmployeeDebtDeducted DECIMAL(18,3) NOT NULL DEFAULT 0");
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
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE())");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='CustomerPackageTransactions')
                CREATE TABLE CustomerPackageTransactions (Id INT IDENTITY PRIMARY KEY,
                CustomerPackageId INT NOT NULL, UsedDate DATETIME NOT NULL DEFAULT GETDATE(),
                EmployeeId INT NULL, Notes NVARCHAR(MAX),
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_CustPkgTrans_CustomerPackages FOREIGN KEY (CustomerPackageId)
                    REFERENCES CustomerPackages(Id) ON DELETE CASCADE,
                CONSTRAINT FK_CustPkgTrans_Employees FOREIGN KEY (EmployeeId)
                    REFERENCES Employees(Id) ON DELETE SET NULL)");
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