using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("Attendance")]
public class AttendanceRecord
{
    [Key]
    public int id { get; set; }
    public int company_id { get; set; }
    public int employee_id { get; set; }
    public DateTime datetime { get; set; }
    public string input_type { get; set; } = "ManualIn";
    public DateTime? input_datetime { get; set; }
}