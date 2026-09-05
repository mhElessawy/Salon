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
        public DbSet<Withdrawal> Withdrawals { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<Salary> Salaries { get; set; }
        public DbSet<EmployeeAdvance> EmployeeAdvances { get; set; }
        public DbSet<Custody> Custodies { get; set; }
        public DbSet<PurchaseRequest> PurchaseRequests { get; set; }
        public DbSet<PurchaseRequestItem> PurchaseRequestItems { get; set; }
        public DbSet<PurchaseRequestCustodyAllocation> PurchaseRequestCustodyAllocations { get; set; }
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
        public DbSet<AppSetting> AppSettings { get; set; }
        public DbSet<AppointmentReminder> AppointmentReminders { get; set; }
        public DbSet<SupplierInvoice> SupplierInvoices { get; set; }
        public DbSet<SupplierInvoiceInstallment> SupplierInvoiceInstallments { get; set; }
        public DbSet<SupplierInvoicePayment> SupplierInvoicePayments { get; set; }
        public DbSet<SupplierInvoiceItem> SupplierInvoiceItems { get; set; }
        public DbSet<Refund> Refunds { get; set; }
        public DbSet<PackageAgreement> PackageAgreements { get; set; }

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

            builder.Entity<Sale>()
                .Property(s => s.GiftForEmployee)
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

            builder.Entity<Withdrawal>()
                .Property(w => w.Amount)
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

            builder.Entity<Salary>()
                .Property(s => s.HadiyaAmount)
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

            builder.Entity<CustomerPackage>()
                .Property(p => p.CurrentBalance)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Shift>()
                .Property(s => s.OpeningBalance)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Shift>()
                .Property(s => s.ClosingBalance)
                .HasColumnType("decimal(18,3)");

            builder.Entity<PurchaseRequest>()
                .Property(p => p.EstimatedAmount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<PurchaseRequest>()
                .Property(p => p.ActualAmount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Custody>()
                .Property(c => c.Amount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<PurchaseRequestCustodyAllocation>()
                .Property(a => a.Amount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<PurchaseRequestCustodyAllocation>()
                .HasOne(a => a.PurchaseRequest)
                .WithMany(p => p.Allocations)
                .HasForeignKey(a => a.PurchaseRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PurchaseRequestCustodyAllocation>()
                .HasOne(a => a.Custody)
                .WithMany(c => c.Allocations)
                .HasForeignKey(a => a.CustodyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SupplierInvoice>()
                .Property(i => i.TotalAmount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<SupplierInvoice>()
                .Property(i => i.DiscountAmount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<SupplierInvoice>()
                .Property(i => i.ExtraExpenses)
                .HasColumnType("decimal(18,3)");

            builder.Entity<SupplierInvoiceInstallment>()
                .Property(i => i.Amount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<SupplierInvoicePayment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<SupplierInvoiceItem>()
                .Property(i => i.UnitPrice)
                .HasColumnType("decimal(18,3)");

            builder.Entity<SupplierInvoiceItem>()
                .Property(i => i.Discount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<SupplierInvoiceItem>()
                .HasOne(i => i.SupplierInvoice)
                .WithMany(inv => inv.Items)
                .HasForeignKey(i => i.SupplierInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SupplierInvoiceItem>()
                .HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SupplierInvoicePayment>()
                .HasOne(p => p.Custody)
                .WithMany(c => c.InvoicePayments)
                .HasForeignKey(p => p.CustodyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SupplierInvoicePayment>()
                .HasOne(p => p.SupplierInvoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(p => p.SupplierInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SupplierInvoiceInstallment>()
                .HasOne(i => i.SupplierInvoice)
                .WithMany(inv => inv.Installments)
                .HasForeignKey(i => i.SupplierInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PurchaseRequest>()
                .HasOne(p => p.SupplierInvoice)
                .WithMany()
                .HasForeignKey(p => p.SupplierInvoiceId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Refund>()
                .Property(r => r.Amount)
                .HasColumnType("decimal(18,3)");

            builder.Entity<Refund>()
                .HasOne(r => r.Sale)
                .WithMany(s => s.Refunds)
                .HasForeignKey(r => r.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PackageAgreement>()
                .Property(a => a.PackagePrice)
                .HasColumnType("decimal(18,3)");

            builder.Entity<PackageAgreement>()
                .Property(a => a.AmountPaid)
                .HasColumnType("decimal(18,3)");

            builder.Entity<PackageAgreement>()
                .HasOne(a => a.CustomerPackage)
                .WithMany()
                .HasForeignKey(a => a.CustomerPackageId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict (�� Cascade) ��� ������ �� Customer/ServicePackage ���� ����� ���� ���
            // ����� ��� CustomerPackageId � �� ������� Cascade ����� ��� ���� �� ���� Cascade
            // ���� ������ (PackageAgreements) �SQL Server ����� ����� ������ (Error 1785)
            builder.Entity<PackageAgreement>()
                .HasOne(a => a.Customer)
                .WithMany()
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PackageAgreement>()
                .HasOne(a => a.ServicePackage)
                .WithMany()
                .HasForeignKey(a => a.ServicePackageId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PackageAgreement>()
                .HasOne(a => a.Sale)
                .WithMany()
                .HasForeignKey(a => a.SaleId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}