using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
    [Table("ShiftBlocks")]
    public class ShiftBlock
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int CompanyId { get; set; }

        [Required]
        [StringLength(100)]
        public string ShiftName { get; set; } = string.Empty;

        [Required]
        public bool IsActive { get; set; } = true;

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ModifiedDate { get; set; }
        
        // Attendance Settings
        [Required]
        public bool IsAttendance { get; set; } = false;
        
        // Single Shift Settings
        public TimeSpan? DefaultInTime { get; set; }
        public TimeSpan? DefaultOutTime { get; set; }
        public TimeSpan? MaxLateCutOffCheckIn { get; set; }
        public TimeSpan? MaxOtCutOffCheckOut { get; set; }
        
        [StringLength(50)]
        public string? DayType1 { get; set; }
        
        [StringLength(50)]
        public string? DayType2 { get; set; }
        
        // Half Day Settings
        public TimeSpan? HalfDayInTime { get; set; }
        public TimeSpan? HalfDayOutTime { get; set; }
        public TimeSpan? HalfDayMaxLateCutOffCheckIn { get; set; }
        public TimeSpan? HalfDayMaxOtCutOffCheckIn { get; set; }
        public TimeSpan? HalfDayEveInTime { get; set; }
        public TimeSpan? HalfDayEveOutTime { get; set; }
        public TimeSpan? HalfDayEveMaxLateCutOffCheckOut { get; set; }
        public TimeSpan? HalfDayEveMaxOtCutOffCheckOut { get; set; }
        
        // Multi Shift Settings
        public TimeSpan? FirstShiftInTime { get; set; }
        public TimeSpan? FirstShiftOutTime { get; set; }
        public TimeSpan? FirstShiftMaxLate { get; set; }
        public TimeSpan? FirstShiftMaxOT { get; set; }
        
        [StringLength(50)]
        public string? FirstShiftDayType1 { get; set; }
        
        [StringLength(50)]
        public string? FirstShiftDayType2 { get; set; }
        
        public TimeSpan? SecondShiftInTime { get; set; }
        public TimeSpan? SecondShiftOutTime { get; set; }
        public TimeSpan? SecondShiftMaxLate { get; set; }
        public TimeSpan? SecondShiftMaxOT { get; set; }
        
        [StringLength(50)]
        public string? SecondShiftDayType1 { get; set; }
        
        [StringLength(50)]
        public string? SecondShiftDayType2 { get; set; }
        
        // Payment Settings
        [Column(TypeName = "decimal(18,2)")]
        public decimal? DayOffRate { get; set; }
        
        [Required]
        public bool IsAttendanceAllowance { get; set; } = false;
        
        // Attendance Allowance Settings
        public int? LeaveCount { get; set; }
        public int? LeaveCount2 { get; set; }
        public int? NoPayCount { get; set; }
        public int? NoPayCount2 { get; set; }
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? LateHoursThreshold { get; set; }
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? AttendanceAllowancePercent { get; set; }
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? AttendanceAllowancePercent2 { get; set; }
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? AttendanceAllowancePercent3 { get; set; }
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? AttendanceAllowancePercent4 { get; set; }
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? AttendanceAllowancePercent5 { get; set; }
        
        // Late Deduction Settings
        [Required]
        public bool IsLateDeduction { get; set; } = false;
        
        [StringLength(20)]
        public string? LateDeductionType { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal? LateDeductRate { get; set; }
        
        public int? LateGracePeriod { get; set; }
        
        // Overtime Settings
        [Required]
        public bool IsOvertimePay { get; set; } = false;
        
        [StringLength(20)]
        public string? OvertimeType { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal? OvertimeRate { get; set; }
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? OtMinimumHours { get; set; }
        
        // Shift Type
        [Required]
        [StringLength(20)]
        public string ShiftType { get; set; } = "Single";

        // Navigation property
        [ForeignKey("CompanyId")]
        public virtual Company? Company { get; set; }
    }
}