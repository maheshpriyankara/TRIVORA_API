using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
    [Table("Company")]
    public class Company
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [StringLength(50)]
        public string? CompanyName { get; set; }

        [StringLength(250)]
        public string? CompanyAddress { get; set; }

        [StringLength(15)]
        public string? TelephonNuber { get; set; }

        [StringLength(15)]
        public string? FaxNumber { get; set; }

        [StringLength(50)]
        public string? EmailId { get; set; }

        [StringLength(50)]
        public string? HrManager { get; set; }

        [StringLength(50)]
        public string? HrContact { get; set; }

        [StringLength(50)]
        public string? HrEmail { get; set; }

        [StringLength(50)]
        public string? EpfActNo { get; set; }

        [StringLength(50)]
        public string? EtfActNp { get; set; }

        [StringLength(50)]
        public string? CompanySector { get; set; }

        [StringLength(50)]
        public string? CompanyRegistrationNumber { get; set; }

        public DateTime? CompanyRegisterd { get; set; }

        public string? CompanyAbout { get; set; }

        [StringLength(15)]
        public string? HalfDayOfWeek { get; set; }
        
        [StringLength(15)]
        public string? HolidayOfWeek { get; set; }

        // ✅ FIXED: Changed from double/string to decimal
        [Column(TypeName = "decimal(18,2)")]
        public decimal MinBasic { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinBudgetaryAllowanceOne { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinBudgetaryAllowanceTwo { get; set; }

        public int MaxEmployeeCount { get; set; }

        public int SalaryStartDate_Office { get; set; }
        public int SalaryStartDate_Wages { get; set; }

        // Navigation property
        public virtual ICollection<ShiftBlock>? ShiftBlocks { get; set; }
    }
}