using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
    public class Employee
    {
        [Key]
        public int Id { get; set; }

        [Column("EmployeeNo")]
        public string? EmployeeNo { get; set; }

        [Column("EPFNo")]
        public string? EPFNo { get; set; }

        [Column("AttendanceId")]
        public string? AttendanceId { get; set; }

        [Required]
        [Column("CompanyId")]
        public int CompanyId { get; set; }

        [Column("Title")]
        public string? Title { get; set; }

        [Column("Initial")]
        public string? Initial { get; set; }

        [Required]
        [Column("FirstName")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Column("LastName")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [Column("SystemName")]
        public string SystemName { get; set; } = string.Empty;

        [Column("NIC")]
        public string? NIC { get; set; }

        [Column("DOB")]
        public DateTime? DOB { get; set; }

        [Column("Gender")]
        public string? Gender { get; set; }

        [Column("MaritalStatus")]
        public string? MaritalStatus { get; set; }

        [Column("BloodGroup")]
        public string? BloodGroup { get; set; }

        [Column("DrivingLicense")]
        public string? DrivingLicense { get; set; }

        [Column("Religion")]
        public string? Religion { get; set; }

        [Column("Nationality")]
        public string? Nationality { get; set; }

        [Column("Race")]
        public string? Race { get; set; }

        [Column("Mobile")]
        public string? Mobile { get; set; }

        [Column("LandPhone")]
        public string? LandPhone { get; set; }

        [Column("ContactNo")]
        public string? ContactNo { get; set; }

        [Column("ResidentialAddress")]
        public string? ResidentialAddress { get; set; }

        [Column("PermanentAddress")]
        public string? PermanentAddress { get; set; }

        [Column("OccupationNo")]
        public int? OccupationNo { get; set; }

        [Column("OccupationGrade")]
        public int? OccupationGrade { get; set; }

        [Column("RoleType")]
        public string? RoleType { get; set; }

        [Column("LeaveApproval")]
        public string? LeaveApproval { get; set; }

        [Column("FinanceApproval")]
        public string? FinanceApproval { get; set; }

        [Column("DesignationId")]
        public int? DesignationId { get; set; }

        [Column("DepartmentId")]
        public int? DepartmentId { get; set; }

        [Column("ShiftBlockId")]
        public int? ShiftBlockId { get; set; }

        [Column("DateOfAppointment")]
        public DateTime? DateOfAppointment { get; set; }

        // Monetary properties - using decimal
        [Required]
        [Column("BasicSalary")]
        public decimal BasicSalary { get; set; }

        [Column("BudgetaryAllowance")]
        public decimal BudgetaryAllowance { get; set; }

        [Column("BudgetaryAllowance2")]
        public decimal BudgetaryAllowance2 { get; set; }

        [Column("AttendanceAllowance")]
        public decimal AttendanceAllowance { get; set; }

        [Column("FixedAllowance")]
        public decimal FixedAllowance { get; set; }

        [Column("MealAllowance")]
        public decimal MealAllowance { get; set; }

        [Column("SpecialAllowance")]
        public decimal SpecialAllowance { get; set; }

        [Column("TransportAllowance")]
        public decimal TransportAllowance { get; set; }

        [Column("AccommodationAllowance")]
        public decimal AccommodationAllowance { get; set; }

        [Column("FuelAllowance")]
        public decimal FuelAllowance { get; set; }

        [Column("CostOfLivingAllowance")]
        public decimal CostOfLivingAllowance { get; set; }

        [Column("PerformanceAllowance")]
        public decimal PerformanceAllowance { get; set; }

        [Column("HealthAllowance")]
        public decimal HealthAllowance { get; set; }

        [Column("EPFPay")]
        public bool EPFPay { get; set; }

        [Column("SalaryPayType")]
        public string? SalaryPayType { get; set; }

        [Column("AccountNumber")]
        public string? AccountNumber { get; set; }

        [Column("BankAccountName")]
        public string? BankAccountName { get; set; }

        [Column("BankCode")]
        public string? BankCode { get; set; }

        [Column("BankName")]
        public string? BankName { get; set; }

        [Column("BranchCode")]
        public string? BranchCode { get; set; }

        [Column("BranchName")]
        public string? BranchName { get; set; }

        [Column("Probation")]
        public bool Probation { get; set; }

        [Column("ProbationPeriod")]
        public int? ProbationPeriod { get; set; }

        [Column("ProbationEndDate")]
        public DateTime? ProbationEndDate { get; set; }

        [Column("ReviewedBy")]
        public string? ReviewedBy { get; set; }

        [Column("Block")]
        public bool Block { get; set; }

        [Column("BlockUntil")]
        public string? BlockUntil { get; set; }

        [Column("BlockReason")]
        public string? BlockReason { get; set; }

        [Column("BlockRemark")]
        public string? BlockRemark { get; set; }

        [Column("Resigned")]
        public bool Resigned { get; set; }

        [Column("ResignedDate")]
        public DateTime? ResignedDate { get; set; }

        [Column("ExitType")]
        public string? ExitType { get; set; }

        [Column("ExitReason")]
        public string? ExitReason { get; set; }

        [Column("ExitRemark")]
        public string? ExitRemark { get; set; }

        [Column("BlockAttendance")]
        public bool BlockAttendance { get; set; }

        [Column("Keen1ContactName")]
        public string? Keen1ContactName { get; set; }

        [Column("Keen1ContactNumber")]
        public string? Keen1ContactNumber { get; set; }

        [Column("Keen1Relationship")]
        public string? Keen1Relationship { get; set; }

        [Column("Keen1Address")]
        public string? Keen1Address { get; set; }

        [Column("Keen1Position")]
        public string? Keen1Position { get; set; }

        [Column("Keen1WorkPlace")]
        public string? Keen1WorkPlace { get; set; }

        [Column("Keen1WorkPlaceContact")]
        public string? Keen1WorkPlaceContact { get; set; }

        [Column("Keen2ContactName")]
        public string? Keen2ContactName { get; set; }

        [Column("Keen2ContactNumber")]
        public string? Keen2ContactNumber { get; set; }

        [Column("Keen2Relationship")]
        public string? Keen2Relationship { get; set; }

        [Column("Keen2Address")]
        public string? Keen2Address { get; set; }

        [Column("Keen2Position")]
        public string? Keen2Position { get; set; }

        [Column("Keen2WorkPlace")]
        public string? Keen2WorkPlace { get; set; }

        [Column("Keen2WorkPlaceContact")]
        public string? Keen2WorkPlaceContact { get; set; }

        [Column("ProfileImage")]
        public string? ProfileImage { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        [Column("CreatedBy")]
        public string? CreatedBy { get; set; }

        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; }

        [Column("ModifiedBy")]
        public string? ModifiedBy { get; set; }

        [Column("ModifiedDate")]
        public DateTime? ModifiedDate { get; set; }

        // Navigation properties
        public virtual ICollection<Qualification>? Qualifications { get; set; } = new List<Qualification>();
        public virtual ICollection<WorkExperience>? WorkExperiences { get; set; } = new List<WorkExperience>();
        public virtual ICollection<LeaveCustomApprover>? LeaveCustomApprovers { get; set; } = new List<LeaveCustomApprover>();
        public virtual ICollection<FinanceCustomApprovers>? FinanceCustomApprovers { get; set; } = new List<FinanceCustomApprovers>();
    }

    public class LeaveCustomApprover
    {
        [Key]
        public int Id { get; set; }

        [Column("EmployeeId")]
        public int EmployeeId { get; set; }

        [Column("ApproverEmployeeId")]
        public int ApproverEmployeeId { get; set; }

        [Column("CompanyId")]
        public int CompanyId { get; set; }

        [Column("CreatedBy")]
        public string? CreatedBy { get; set; }

        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }

        [ForeignKey("ApproverEmployeeId")]
        public virtual Employee? ApproverEmployee { get; set; }
    }

    public class FinanceCustomApprovers
    {
        [Key]
        public int Id { get; set; }

        [Column("EmployeeId")]
        public int EmployeeId { get; set; }

        [Column("ApproverEmployeeId")]
        public int ApproverEmployeeId { get; set; }

        [Column("CompanyId")]
        public int CompanyId { get; set; }

        [Column("CreatedBy")]
        public string? CreatedBy { get; set; }

        [Column("CreatedDate")]
        public DateTime CreatedDate { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }

        [ForeignKey("ApproverEmployeeId")]
        public virtual Employee? ApproverEmployee { get; set; }
    }
}