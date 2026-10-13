using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
    [Table("FinanceCustomApprovers")]
    public class FinanceCustomApprovers
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int ApproverEmployeeId { get; set; }

        [Required]
        public int CompanyId { get; set; }

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? CreatedDate { get; set; }

        public bool? IsActive { get; set; }

        // Navigation properties (optional — only if you want EF to load related Employees)
        [ForeignKey(nameof(EmployeeId))]
        public virtual Employee? Employee { get; set; }

        [ForeignKey(nameof(ApproverEmployeeId))]
        public virtual Employee? ApproverEmployee { get; set; }

        [ForeignKey(nameof(CompanyId))]
        public virtual Company? Company { get; set; }
    }
}