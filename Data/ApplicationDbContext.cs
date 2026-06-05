using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Salon.Models;

namespace Salon.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<ServiceCategory> ServiceCategories { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<AppointmentService> AppointmentServices { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Deposit> Deposits { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<Salary> Salaries { get; set; }
        public DbSet<EmployeeAdvance> EmployeeAdvances { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<AttendancePermission> AttendancePermissions { get; set; }
        public DbSet<ServicePackage> ServicePackages { get; set; }
        public DbSet<CustomerPackage> CustomerPackages { get; set; }
        public DbSet<CustomerPackageTransaction> CustomerPackageTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Sale>()
                .Property(s => s.TotalAmount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Sale>()
                .Property(s => s.Discount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Sale>()
                .Property(s => s.NetAmount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Sale>()
                .Property(s => s.EmployeeGift)
                .HasColumnType("decimal(18,3)");

            builder.Entity<SaleItem>()
                .Property(s => s.Price)
                .HasColumnType("decimal(18,3)");

            builder.Entity<SaleItem>()
                .Property(s => s.Total)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Service>()
                .Property(s => s.Price)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Product>()
                .Property(p => p.PurchasePrice)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Product>()
                .Property(p => p.SalePrice)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Expense>()
                .Property(e => e.Amount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Deposit>()
                .Property(d => d.Amount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Employee>()
                .Property(e => e.BasicSalary)
                .HasColumnName("BasicSalary")
                .HasColumnType("decimal(18,3)");

            // Configure Employee?Department using reflection so it works
            // regardless of the navigation property name (DepartmentRef, DepartmentNav, etc.)
            var deptNavProp = typeof(Employee)
                .GetProperties()
                .FirstOrDefault(p => p.PropertyType == typeof(Department));

            if (deptNavProp != null)
            {
                builder.Entity<Employee>()
                    .HasOne<Department>(deptNavProp.Name)
                    .WithMany(d => d.Employees)
                    .HasForeignKey(e => e.DepartmentId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);
            }
            else
            {
                builder.Entity<Department>()
                    .HasMany(d => d.Employees)
                    .WithOne()
                    .HasForeignKey(e => e.DepartmentId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);
            }

            builder.Entity<Salary>()
                .Property(s => s.BasicSalary)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Salary>()
                .Property(s => s.Allowances)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Salary>()
                .Property(s => s.Deductions)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Salary>()
                .Property(s => s.AdvanceDeducted)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Salary>()
                .Property(s => s.NetSalary)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Salary>()
                .Property(s => s.GiftAmount)
                .HasColumnType("decimal(18,3)")
                .IsRequired(false);

            builder.Entity<EmployeeAdvance>()
                .Property(a => a.Amount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<EmployeeAdvance>()
                .Property(a => a.DeductedAmount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<ServicePackage>()
                .Property(p => p.Price)
                .HasColumnType("decimal(18,3)");

            builder.Entity<CustomerPackage>()
                .Property(p => p.PricePaid)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Shift>()
                .Property(s => s.OpeningBalance)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Shift>()
                .Property(s => s.ClosingBalance)
                .HasColumnType("decimal(18,3)");
        }
    }
}