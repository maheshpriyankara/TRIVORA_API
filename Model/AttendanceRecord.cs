using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
    [Table("Attendance")]
    public class AttendanceRecord
    {
        [Key]
        public int id { get; set; }
        
        public int company_id { get; set; }
        public int employee_id { get; set; }
        public DateTime datetime { get; set; }
        public string input_type { get; set; } = "Manual";
        public DateTime? input_datetime { get; set; }
    }
}