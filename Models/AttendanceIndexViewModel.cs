namespace Salon.Models
{
    public class AttendanceIndexRow
    {
        public Employee Employee { get; set; } = null!;
        public Attendance? Record { get; set; }
        public bool HasAttendance => Record != null;

        /// <summary>
        /// موظف سجّل الحضور امبارح بالليل ولسه لم ينصرف — سجله من اليوم السابق
        /// </summary>
        public bool IsOvernight { get; set; }
    }
}
