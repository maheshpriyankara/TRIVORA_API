namespace TRIVORA_API.Models
{
    public class AttendanceManualLog
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? RequestData { get; set; }
        public string ResponseStatus { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
        public string? IpAddress { get; set; }
        public int? UserId { get; set; }
    }
}