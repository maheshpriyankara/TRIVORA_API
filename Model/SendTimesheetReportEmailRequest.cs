public class SendTimesheetReportEmailRequest
{
    public int CompanyId { get; set; }
    public string Scope { get; set; } = "selected"; // selected | all | lines
    public int? EmployeeId { get; set; }
    public int? DepartmentId { get; set; }

    public string ToEmail { get; set; } = "";
    public string? CcEmail { get; set; }    // comma-separated
    public string? BccEmail { get; set; }   // optional

    // Period info
    public int FromYear { get; set; }
    public string FromMonthName { get; set; } = "";
    public int ToYear { get; set; }
    public string ToMonthName { get; set; } = "";

    // Full rendered HTML for the report (built by the front-end Print preview)
    public string HtmlBody { get; set; } = "";

    public string? CustomMessage { get; set; }
}