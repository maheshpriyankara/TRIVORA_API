// Models/Employee.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace TRIVORA_API.Models
{
    public class Employee
    {
        [Key]
        public Int32 EPFNo { get; set; }
        public string EmployeeNo { get; set; }

        // Personal Data
        public string Title { get; set; }
        public string Initial { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string SystemName { get; set; }
        public DateTime? DOB { get; set; }
        public string Gender { get; set; }
        public string NIC { get; set; }
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

        // Payroll Data
        public int? CompanyId { get; set; }
        public int? DesignationId { get; set; }
        public int? DepartmentId { get; set; }
        public int? ShiftBlockId { get; set; }
        public DateTime? DateOfAppointment { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal BudgetaryAllowance { get; set; }
        public decimal AttendanceAllowance { get; set; }
        public string AttendanceId { get; set; }
        public bool EPFPay { get; set; }
        public bool Resigned { get; set; }
        public DateTime? ResignedDate { get; set; }
        public bool BlockAttendance { get; set; }

        // Allowances
        public decimal FixedAllowance { get; set; }
        public decimal MealAllowance { get; set; }
        public decimal SpecialAllowance { get; set; }
        public decimal TheaterAllowance { get; set; }
        public decimal ICUAllowance { get; set; }
        public decimal TransportAllowance { get; set; }
        public decimal AccommodationAllowance { get; set; }
        public decimal FuelAllowance { get; set; }
        public decimal Allowance2 { get; set; }

        // Bank Details
        public string PaymentMethod { get; set; }
        public string AccountNo { get; set; } = string.Empty;
        public string BankCode { get; set; }
        public string BranchCode { get; set; }

        // Emergency Contact
        public string EmergencyContactPerson { get; set; }
        public string EmergencyContactNumber { get; set; }
        public string EmergencyContactAddress { get; set; }
        public string EmergencyContactRelationship { get; set; }
    }
}