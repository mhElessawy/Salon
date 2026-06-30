using System.ComponentModel.DataAnnotations;

namespace Salon.Models
{
    public class AppSetting
    {
        [Key]
        [MaxLength(100)]
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
    }
}
