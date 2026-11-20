// Models/PaySheet.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
    [Table("Paysheet")]
    public class PaySheet
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("EmployeeID")]
        public int EmployeeId { get; set; }

        [Column("Period")]
        [MaxLength(20)]
        public string Period { get; set; } = string.Empty;

        // ── EARNINGS ──

        [Column("BasicSalary")]
        public decimal BasicSalary { get; set; }

        [Column("BudgetaryAllowance")]
        public decimal BudgetaryAllowance { get; set; }

        [Column("BudgetaryAllowance2")]
        public decimal BudgetaryAllowance2 { get; set; }

        [Column("SalaryForEPF")]
        public decimal SalaryForEPF { get; set; }

        // ── WORKING / LEAVE ──

        [Column("WorkingDays")]
        public double WorkingDays { get; set; }

        [Column("WorkingHours")]
        public double WorkingHours { get; set; }

        [Column("LeaveDays")]
        public double LeaveDays { get; set; }

        // ── OVERTIME ──

        [Column("NoramlOverTimePay")]
        public decimal NoramlOverTimePay { get; set; }           // note: DB column has typo "Noraml"

        [Column("NoramlOverTimePoyaHours")]
        public double NoramlOverTimePoyaHours { get; set; }

        [Column("WeeklyDayOffOverTimePay")]
        public decimal WeeklyDayOffOverTimePay { get; set; }

        [Column("WeeklyDayOffOverTimeHours")]
        public double WeeklyDayOffOverTimeHours { get; set; }

        [Column("WeeklyHalfDayOverTimePay")]
        public decimal WeeklyHalfDayOverTimePay { get; set; }

        [Column("WeeklyHalfDayOverTimeHours")]
        public double WeeklyHalfDayOverTimeHours { get; set; }

        [Column("PoyaDayOverTimePay")]
        public decimal PoyaDayOverTimePay { get; set; }

        [Column("PoyaDayOverTimeHours")]
        public double PoyaDayOverTimeHours { get; set; }

        [Column("BankHolidayOverTimePay")]
        public decimal BankHolidayOverTimePay { get; set; }

        [Column("BankHolidayOverTimeHours")]
        public double BankHolidayOverTimeHours { get; set; }

        [Column("MercantileHolidayOverTimePay")]
        public decimal MercantileHolidayOverTimePay { get; set; }

        [Column("MercantileHolidayOverTimeHours")]
        public double MercantileHolidayOverTimeHours { get; set; }

        // nullable in DB
        [Column("ExtraOverTimePay")]
        public decimal? ExtraOverTimePay { get; set; }

        [Column("ExtraOverTimeHours")]
        public double? ExtraOverTimeHours { get; set; }

        // ── ALLOWANCES ──

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

        [Column("OtherAllowances")]
        public decimal OtherAllowances { get; set; }

        [Column("AttendanceAllowance")]
        public decimal AttendanceAllowance { get; set; }

        [Column("TotalEarning")]
        public decimal TotalEarning { get; set; }

        [Column("GrossSalary")]
        public decimal GrossSalary { get; set; }

        // ── DEDUCTIONS ──

        [Column("PayCutHours")]
        public double PayCutHours { get; set; }

        [Column("PayCutDeduction")]
        public decimal PayCutDeduction { get; set; }

        [Column("NoPayCount")]
        public double NoPayCount { get; set; }

        [Column("NoPayDeduction")]
        public decimal NoPayDeduction { get; set; }

        [Column("PayeeDeduction")]
        public decimal PayeeDeduction { get; set; }

        [Column("Epf8")]
        public decimal Epf8 { get; set; }

        [Column("CashShortDeduction")]
        public decimal CashShortDeduction { get; set; }

        [Column("WelfareDeduction")]
        public decimal WelfareDeduction { get; set; }

        [Column("Loan")]
        public decimal Loan { get; set; }

        [Column("SalaryAdvance")]
        public decimal SalaryAdvance { get; set; }

        [Column("OtherDeduction")]
        public decimal OtherDeduction { get; set; }

        [Column("TotalDeduction")]
        public decimal TotalDeduction { get; set; }

        [Column("NetPay")]
        public decimal NetPay { get; set; }

        // ── EMPLOYER CONTRIBUTIONS ──

        [Column("Epf12")]
        public decimal Epf12 { get; set; }

        [Column("Etf3")]
        public decimal Etf3 { get; set; }

        // ── NAVIGATION ──

        [ForeignKey(nameof(EmployeeId))]
        public virtual Employee? Employee { get; set; }
    }

    // ============================================================
    // DTOs
    // ============================================================

    public class PaySheetDto
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeNo { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;

        // earnings
        public decimal BasicSalary { get; set; }
        public decimal BudgetaryAllowance { get; set; }
        public decimal BudgetaryAllowance2 { get; set; }
        public decimal SalaryForEPF { get; set; }

        // working / leave
        public double WorkingDays { get; set; }
        public double WorkingHours { get; set; }
        public double LeaveDays { get; set; }

        // overtime
        public decimal NoramlOverTimePay { get; set; }
        public double NoramlOverTimePoyaHours { get; set; }
        public decimal WeeklyDayOffOverTimePay { get; set; }
        public double WeeklyDayOffOverTimeHours { get; set; }
        public decimal WeeklyHalfDayOverTimePay { get; set; }
        public double WeeklyHalfDayOverTimeHours { get; set; }
        public decimal PoyaDayOverTimePay { get; set; }
        public double PoyaDayOverTimeHours { get; set; }
        public decimal BankHolidayOverTimePay { get; set; }
        public double BankHolidayOverTimeHours { get; set; }
        public decimal MercantileHolidayOverTimePay { get; set; }
        public double MercantileHolidayOverTimeHours { get; set; }
        public decimal? ExtraOverTimePay { get; set; }
        public double? ExtraOverTimeHours { get; set; }

        // allowances
        public decimal FixedAllowance { get; set; }
        public decimal MealAllowance { get; set; }
        public decimal SpecialAllowance { get; set; }
        public decimal TransportAllowance { get; set; }
        public decimal AccommodationAllowance { get; set; }
        public decimal FuelAllowance { get; set; }
        public decimal CostOfLivingAllowance { get; set; }
        public decimal PerformanceAllowance { get; set; }
        public decimal HealthAllowance { get; set; }
        public decimal OtherAllowances { get; set; }
        public decimal AttendanceAllowance { get; set; }
        public decimal TotalEarning { get; set; }
        public decimal GrossSalary { get; set; }

        // deductions
        public double PayCutHours { get; set; }
        public decimal PayCutDeduction { get; set; }
        public double NoPayCount { get; set; }
        public decimal NoPayDeduction { get; set; }
        public decimal PayeeDeduction { get; set; }
        public decimal Epf8 { get; set; }
        public decimal CashShortDeduction { get; set; }
        public decimal WelfareDeduction { get; set; }
        public decimal Loan { get; set; }
        public decimal SalaryAdvance { get; set; }
        public decimal OtherDeduction { get; set; }
        public decimal TotalDeduction { get; set; }
        public decimal NetPay { get; set; }

        // employer contributions
        public decimal Epf12 { get; set; }
        public decimal Etf3 { get; set; }

        // extra
        public string? ProfileImage { get; set; }
    }

    public class PaySheetRequestDto
    {
        public int EmployeeId { get; set; }
        public string Period { get; set; } = string.Empty;

        // earnings
        public decimal BasicSalary { get; set; }
        public decimal BudgetaryAllowance { get; set; }
        public decimal BudgetaryAllowance2 { get; set; }
        public decimal SalaryForEPF { get; set; }

        // working / leave
        public double WorkingDays { get; set; }
        public double WorkingHours { get; set; }
        public double LeaveDays { get; set; }

        // overtime
        public decimal NoramlOverTimePay { get; set; }
        public double NoramlOverTimePoyaHours { get; set; }
        public decimal WeeklyDayOffOverTimePay { get; set; }
        public double WeeklyDayOffOverTimeHours { get; set; }
        public decimal WeeklyHalfDayOverTimePay { get; set; }
        public double WeeklyHalfDayOverTimeHours { get; set; }
        public decimal PoyaDayOverTimePay { get; set; }
        public double PoyaDayOverTimeHours { get; set; }
        public decimal BankHolidayOverTimePay { get; set; }
        public double BankHolidayOverTimeHours { get; set; }
        public decimal MercantileHolidayOverTimePay { get; set; }
        public double MercantileHolidayOverTimeHours { get; set; }
        public decimal? ExtraOverTimePay { get; set; }
        public double? ExtraOverTimeHours { get; set; }

        // allowances
        public decimal FixedAllowance { get; set; }
        public decimal MealAllowance { get; set; }
        public decimal SpecialAllowance { get; set; }
        public decimal TransportAllowance { get; set; }
        public decimal AccommodationAllowance { get; set; }
        public decimal FuelAllowance { get; set; }
        public decimal CostOfLivingAllowance { get; set; }
        public decimal PerformanceAllowance { get; set; }
        public decimal HealthAllowance { get; set; }
        public decimal OtherAllowances { get; set; }
        public decimal AttendanceAllowance { get; set; }
        public decimal TotalEarning { get; set; }
        public decimal GrossSalary { get; set; }

        // deductions
        public double PayCutHours { get; set; }
        public decimal PayCutDeduction { get; set; }
        public double NoPayCount { get; set; }
        public decimal NoPayDeduction { get; set; }
        public decimal PayeeDeduction { get; set; }
        public decimal Epf8 { get; set; }
        public decimal CashShortDeduction { get; set; }
        public decimal WelfareDeduction { get; set; }
        public decimal Loan { get; set; }
        public decimal SalaryAdvance { get; set; }
        public decimal OtherDeduction { get; set; }
        public decimal TotalDeduction { get; set; }
        public decimal NetPay { get; set; }

        // employer contributions
        public decimal Epf12 { get; set; }
        public decimal Etf3 { get; set; }
    }
}