using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Salon.Models;

namespace Salon.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var userManager  = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager  = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var context      = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // Create roles
            string[] roles = { "Admin", "Manager", "Cashier", "Employee" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Create default admin
            var adminEmail = "admin@salon.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName     = adminEmail,
                    Email        = adminEmail,
                    FullName     = "بداح العجمي",
                    EmailConfirmed = true,
                    IsActive     = true,
                    UserDepartment = null  // Admin sees all
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            // Seed default departments
            if (!await context.Departments.AnyAsync())
            {
                context.Departments.AddRange(
                    new Department { Name = "حلاقة",   Description = "قسم الحلاقة" },
                    new Department { Name = "مساج",    Description = "قسم المساج" },
                    new Department { Name = "إدارة",   Description = "الإدارة العامة" },
                    new Department { Name = "محاسبة",  Description = "القسم المالي" },
                    new Department { Name = "نظافة",   Description = "قسم النظافة" },
                    new Department { Name = "أمن",     Description = "قسم الأمن" }
                );
                await context.SaveChangesAsync();
            }

            // Seed two default service categories (Barber + Massage)
            if (!await context.ServiceCategories.AnyAsync())
            {
                context.ServiceCategories.AddRange(
                    new ServiceCategory
                    {
                        Name       = "حلاقة",
                        Department = "حلاقة",
                        Icon       = "fas fa-cut",
                        Color      = "#F7941D",
                        IsActive   = true
                    },
                    new ServiceCategory
                    {
                        Name       = "مساج",
                        Department = "مساج",
                        Icon       = "fas fa-spa",
                        Color      = "#17a2b8",
                        IsActive   = true
                    }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}
