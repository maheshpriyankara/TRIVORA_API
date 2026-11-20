public class SendAttendanceReportEmailRequest
{
    public int CompanyId { get; set; }
    public string Scope { get; set; } = "selected";   // selected | all | lines
    public int? EmployeeId { get; set; }
    public int? DepartmentId { get; set; }

    public string ToEmail { get; set; } = "";
    public string? CcEmail { get; set; }
    public string? BccEmail { get; set; }

    public int FromYear { get; set; }
    public string FromMonthName { get; set; } = "";
    public int ToYear { get; set; }
    public string ToMonthName { get; set; } = "";

    // Generic fields so ONE endpoint serves ALL report types
    public string ReportTitle { get; set; } = "Attendance Report";
    public string ReportFileNamePrefix { get; set; } = "Attendance_Report";

    public string HtmlBody { get; set; } = "";
    public string? CustomMessage { get; set; }
}