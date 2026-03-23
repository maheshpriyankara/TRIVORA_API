using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
    [Table("Leave")]
    public class Leave
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int CompanyId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public DateTime LeaveDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string LeaveType { get; set; } = string.Empty;

        public bool IsHalfDay { get; set; } = false;

        [MaxLength(500)]
        public string? Remarks { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public int? CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public int? ModifiedBy { get; set; }

        public DateTime? ModifiedDate { get; set; }

        // Add these missing properties that exist in your database
        public int? CancelledBy { get; set; }

        public DateTime? CancelledDate { get; set; }
    }
}