namespace TrivoraHrisBackend.Models
{
    public class SendPayrollEmailRequest
    {
        public int EmployeeId { get; set; }
        public string? EmployeeNo { get; set; }
        public string? EmployeeName { get; set; }
        public string? Period { get; set; }         // e.g. "January 2025"
        public string? Month { get; set; }
        public int Year { get; set; }

        public string Email { get; set; } = string.Empty;
        public string? CcEmail { get; set; }
        public string? BccEmail { get; set; }
        public string? Message { get; set; }

        public bool SendPaysheet { get; set; }
        public bool SendTimesheet { get; set; }

        public string? PaysheetHtmlBody { get; set; }
        public string? TimesheetHtmlBody { get; set; }
    }

    public class SendPayrollEmailResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<string> Sent { get; set; } = new();
        public List<string> Failed { get; set; } = new();
    }
}