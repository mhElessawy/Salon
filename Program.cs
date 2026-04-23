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
        options.UseSqlServer(connStr);
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
        }
        else
        {
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='EmployeeAdvances' AND COLUMN_NAME='AmountPaid') ALTER TABLE EmployeeAdvances ADD AmountPaid DECIMAL(18,3) NOT NULL DEFAULT 0");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Products' AND COLUMN_NAME='SupplierId') ALTER TABLE Products ADD SupplierId INT NULL");
            TryExec("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Products' AND COLUMN_NAME='OpeningQuantity') ALTER TABLE Products ADD OpeningQuantity INT NOT NULL DEFAULT 0");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Suppliers')
                CREATE TABLE Suppliers (Id INT IDENTITY PRIMARY KEY, Name NVARCHAR(200) NOT NULL,
                Phone NVARCHAR(50), Email NVARCHAR(200), Address NVARCHAR(500), Notes NVARCHAR(MAX),
                IsActive BIT NOT NULL DEFAULT 1, CreatedAt DATETIME NOT NULL DEFAULT GETDATE())");
            TryExec(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='StockMovements')
                CREATE TABLE StockMovements (Id INT IDENTITY PRIMARY KEY, ProductId INT NOT NULL,
                MovementType NVARCHAR(50) NOT NULL DEFAULT N'??????', Quantity INT NOT NULL,
                UnitPrice DECIMAL(18,3) NOT NULL DEFAULT 0, EmployeeId INT NULL, SupplierId INT NULL,
                Notes NVARCHAR(MAX), MovementDate DATE NOT NULL DEFAULT GETDATE(), CreatedAt DATETIME NOT NULL DEFAULT GETDATE())");
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