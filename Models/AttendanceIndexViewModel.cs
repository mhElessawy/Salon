namespace Salon.Models
{
    public class AttendanceIndexRow
    {
        public Employee Employee { get; set; } = null!;

        /// <summary>أحدث سجل نشط (بدون انصراف) أو آخر سجل اليوم</summary>
        public Attendance? Record { get; set; }

        /// <summary>كل سجلات الموظف في اليوم (وردية واحدة أو أكثر)</summary>
        public List<Attendance> AllRecords { get; set; } = new();

        public bool HasAttendance => Record != null;

        /// <summary>موظف سجّل حضور امبارح بالليل ولسه لم ينصرف</summary>
        public bool IsOvernight { get; set; }
    }
}