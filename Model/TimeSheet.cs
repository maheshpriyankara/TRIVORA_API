using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
    [Table("TimeSheet")]
    public class TimeSheet
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        [Required]
        public int CompanyId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        // Single shift fields

        // Multi-shift fields - First Shift
        public DateTime? FirstInDate { get; set; }
        public TimeSpan? FirstInTime { get; set; }
        public DateTime? FirstOutDate { get; set; }
        public TimeSpan? FirstOutTime { get; set; }

        // Multi-shift fields - Second Shift
        public DateTime? SecondInDate { get; set; }
        public TimeSpan? SecondInTime { get; set; }
        public DateTime? SecondOutDate { get; set; }
        public TimeSpan? SecondOutTime { get; set; }

        // Alternative: You can also use the existing fields for multi-shift
        // Shift block references
        public int? FirstShiftBlockId { get; set; }
        public int? SecondShiftBlockId { get; set; }

        // Calculated fields
        public TimeSpan? LateHours { get; set; }
        public TimeSpan? OtHoursNormal { get; set; }
        public TimeSpan? OtHoursDouble { get; set; }
        public TimeSpan? OtHoursExtra { get; set; }
        public TimeSpan? WorkHours { get; set; }

        // Day and pay type
        public string DayType { get; set; } = "Normal";
        public string PayType { get; set; } = "Regular";
    }
}