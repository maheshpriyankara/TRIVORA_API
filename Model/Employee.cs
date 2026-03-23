using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TRIVORA_API.Models;

namespace TRIVORA_API.Models
{
    public class Employee
    {
        public int Id { get; set; }

        public string EmployeeNo { get; set; }

        public string EPFNo { get; set; }

        public string AttendanceId { get; set; }

        [Required]
        public int CompanyId { get; set; }

        public string Title { get; set; }
        public string Initial { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        public string SystemName { get; set; }

        public string NIC { get; set; }
        public DateTime? DOB { get; set; }
        public string Gender { get; set; }
        public string MaritalStatus { get; set; }
        public string BloodGroup { get; set; }
        public string DrivingLicense { get; set; }
        public string Religion { get; set; }
        public string Nationality { get; set; }
        public string Race { get; set; }
        public string Mobile { get; set; }
        public string LandPhone { get; set; }
        public string ContactNo { get; set; }
        public string ResidentialAddress { get; set; }
        public string PermanentAddress { get; set; }

        // New fields
        public int? OccupationNo { get; set; }
        public int? OccupationGrade { get; set; }
        public string RoleType { get; set; } = "Standard Employee";
        public string LeaveApproval { get; set; } = "Super Admin";
        public string FinanceApproval { get; set; } = "Super Admin";

        [Required]
        public int? DesignationId { get; set; }

        [Required]
        public int? DepartmentId { get; set; }
        [Required]
        public int? ShiftBlockId { get; set; }
        public DateTime? DateOfAppointment { get; set; }

        [Required]
        public decimal BasicSalary { get; set; }

        public decimal BudgetaryAllowance { get; set; }
        public decimal BudgetaryAllowance2 { get; set; }
        public decimal AttendanceAllowance { get; set; }
        public decimal FixedAllowance { get; set; }
        public decimal MealAllowance { get; set; }
        public decimal SpecialAllowance { get; set; }
        public decimal TransportAllowance { get; set; }
        public decimal AccommodationAllowance { get; set; }
        public decimal FuelAllowance { get; set; }
        public decimal CostOfLivingAllowance { get; set; }
        public decimal PerformanceAllowance { get; set; }
        public decimal HealthAllowance { get; set; }

        public bool EPFPay { get; set; }
        public string SalaryPayType { get; set; } = "CashPay";
        public string AccountNumber { get; set; }
        public string BankAccountName { get; set; }
        public string BankCode { get; set; }
        public string BankName { get; set; }
        public string BranchCode { get; set; }
        public string BranchName { get; set; }

        public bool Probation { get; set; }
        public int? ProbationPeriod { get; set; }
        public DateTime? ProbationEndDate { get; set; }
        public string ReviewedBy { get; set; }

        public bool Block { get; set; }
        public string BlockUntil { get; set; }
        public string BlockReason { get; set; }
        public string BlockRemark { get; set; }

        public bool Resigned { get; set; }
        public DateTime? ResignedDate { get; set; }
        public string ExitType { get; set; }
        public string ExitReason { get; set; }
        public string ExitRemark { get; set; }
        public bool BlockAttendance { get; set; }

        public string Keen1ContactName { get; set; }
        public string Keen1ContactNumber { get; set; }
        public string Keen1Relationship { get; set; }
        public string Keen1Address { get; set; }
        public string Keen1Position { get; set; }
        public string Keen1WorkPlace { get; set; }
        public string Keen1WorkPlaceContact { get; set; }

        public string Keen2ContactName { get; set; }
        public string Keen2ContactNumber { get; set; }
        public string Keen2Relationship { get; set; }
        public string Keen2Address { get; set; }
        public string Keen2Position { get; set; }
        public string Keen2WorkPlace { get; set; }
        public string Keen2WorkPlaceContact { get; set; }

        public string ProfileImage { get; set; }
        public bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        // Navigation properties
        public List<Qualification> Qualifications { get; set; } = new List<Qualification>();
        public List<WorkExperience> WorkExperiences { get; set; } = new List<WorkExperience>();
        public List<LeaveCustomApprover> LeaveCustomApprovers { get; set; } = new List<LeaveCustomApprover>();
        public List<FinanceCustomApprover> FinanceCustomApprovers { get; set; } = new List<FinanceCustomApprover>();
    }
}

public class LeaveCustomApprover
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public int ApproverEmployeeId { get; set; }
        public int CompanyId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public Employee Employee { get; set; }
        public Employee ApproverEmployee { get; set; }
    }

    public class FinanceCustomApprover
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public int ApproverEmployeeId { get; set; }
        public int CompanyId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public Employee Employee { get; set; }
        public Employee ApproverEmployee { get; set; }
    }