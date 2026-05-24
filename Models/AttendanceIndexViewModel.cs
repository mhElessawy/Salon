namespace Salon.Models
{
    public class AttendanceIndexRow
    {
        public Employee Employee { get; set; } = null!;
        public Attendance? Record { get; set; }
        public bool HasAttendance => Record != null;
    }
}
