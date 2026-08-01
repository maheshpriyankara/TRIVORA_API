public class ManualAttendanceEntryDto
{
    public int EmployeeId { get; set; }
    public DateTime Date { get; set; }
    public string InTime { get; set; }
    public string OutTime { get; set; }
    public DateTime? OutDate { get; set; }
    public string ShiftType { get; set; }      // "Single" or "Multi"
    public int? ShiftNumber { get; set; }      // 1 or 2
    public int? ShiftBlockId { get; set; }
}