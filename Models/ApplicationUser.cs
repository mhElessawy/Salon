using Microsoft.AspNetCore.Identity;

namespace Salon.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        /// <summary>Department: "Barber" | "Massage" | "All" | null (Admin/Manager)</summary>
        public string? UserDepartment { get; set; }

        /// <summary>Link user to Employee record (for Employee type users)</summary>
        public int? LinkedEmployeeId { get; set; }
    }
}
