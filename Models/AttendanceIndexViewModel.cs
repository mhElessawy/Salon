namespace Salon.Models
{
    public class AttendanceIndexRow
    {
        public Employee Employee { get; set; } = null!;

        /// <summary>Latest active record (no check-out) or last record today</summary>
        public Attendance? Record { get; set; }

        /// <summary>All employee records for the day (one or more shifts)</summary>
        public List<Attendance> AllRecords { get; set; } = new();

        public bool HasAttendance => Record != null;

        /// <summary>Employee checked in yesterday night and has not checked out yet</summary>
        public bool IsOvernight { get; set; }
    }
}