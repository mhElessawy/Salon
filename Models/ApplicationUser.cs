using Microsoft.AspNetCore.Identity;

namespace Salon.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        /// <summary>«·ﬁ”„: "Õ·«ﬁ…" | "„”«Ã" | "«·ﬂ·" | null (Admin/Manager)</summary>
        public string? UserDepartment { get; set; }

        /// <summary>—»ÿ «·„” Œœ„ »”Ã· „ÊŸ› (··„” Œœ„Ì‰ „‰ ‰Ê⁄ Employee)</summary>
        public int? LinkedEmployeeId { get; set; }
    }
}