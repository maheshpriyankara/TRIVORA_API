using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
    [Table("Company")] // Map to your existing table name
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

         public double? MinBasic { get; set; }
        public string? MinBudgetaryAllowanceOne { get; set; }
        public string? MinBudgetaryAllowanceTwo { get; set; }

        // Navigation property
        public virtual ICollection<ShiftBlock>? ShiftBlocks { get; set; }
    }
}