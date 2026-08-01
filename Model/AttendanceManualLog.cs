using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("AttendanceManualLog")]
public class AttendanceManualLog
{
    [Key]
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string Action { get; set; }
    public string RequestData { get; set; }
    public string ResponseStatus { get; set; }
    public string ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
    public string IpAddress { get; set; }
    public int? UserId { get; set; }
}