using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
    [Table("EmployeeAuditLogs")]
    public class EmployeeAuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Operation { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Source { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string PerformedBy { get; set; } = string.Empty;

        [Required]
        public DateTime PerformedDate { get; set; }

        // nvarchar(max) — no MaxLength attribute
        public string? RowData { get; set; }

        public string? Changes { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Navigation (optional)
        [ForeignKey(nameof(EmployeeId))]
        public virtual Employee? Employee { get; set; }
    }
}