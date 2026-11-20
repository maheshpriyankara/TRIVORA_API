using System;

namespace TrivoraHrisBackend.Models
{
    public class PaySheetPrintDto
    {
        // ═══════════════════════════════════════════════════════════
        // IDENTIFIERS
        // ═══════════════════════════════════════════════════════════
        public int      PaySheetId        { get; set; }
        public int      EmployeeId        { get; set; }
        public int      CompanyId         { get; set; }
        public int      DepartmentId      { get; set; }
        public int      DesignationId     { get; set; }
        public int      ShiftBlockId      { get; set; }

        // ═══════════════════════════════════════════════════════════
        // PAYROLL PERIOD / EMPLOYMENT FLAGS
        // ═══════════════════════════════════════════════════════════
        public string   Period            { get; set; } = "";
        public bool     EpfPay            { get; set; }
        public string   SalaryPayType     { get; set; } = "Bank";
        public string   Nic               { get; set; } = "";

        // ═══════════════════════════════════════════════════════════
        // EMPLOYEE (joined)
        // ═══════════════════════════════════════════════════════════
        public string   EmployeeName      { get; set; } = "";
        public string   EmployeeNo        { get; set; } = "";
        public string   EpfNo             { get; set; } = "";
        public string   ProfileImage      { get; set; } = "";

        // ═══════════════════════════════════════════════════════════
        // COMPANY (joined)
        // ═══════════════════════════════════════════════════════════
        public string   CompanyName       { get; set; } = "";
        public string   CompanyAddress    { get; set; } = "";

        // ═══════════════════════════════════════════════════════════
        // DEPARTMENT / DESIGNATION (joined)
        // ═══════════════════════════════════════════════════════════
        public string   Department        { get; set; } = "";
        public string   Designation       { get; set; } = "";

        // ═══════════════════════════════════════════════════════════
        // BANK (from payroll)
        // ═══════════════════════════════════════════════════════════
        public string   BankName          { get; set; } = "";
        public string   BranchName        { get; set; } = "";
        public string   BankCode          { get; set; } = "";
        public string   BranchCode        { get; set; } = "";
        public string   AccountNumber     { get; set; } = "";

        // ═══════════════════════════════════════════════════════════
        // EARNINGS
        // ═══════════════════════════════════════════════════════════
        public decimal  BasicSalary            { get; set; }
        public decimal  BudgetaryAllowance     { get; set; }
        public decimal  BudgetaryAllowance2    { get; set; }
        public decimal  AttendanceAllowance    { get; set; }
        public decimal  FixedAllowance         { get; set; }
        public decimal  MealAllowance          { get; set; }
        public decimal  SpecialAllowance       { get; set; }
        public decimal  TransportAllowance     { get; set; }
        public decimal  AccommodationAllowance { get; set; }
        public decimal  FuelAllowance          { get; set; }
        public decimal  CostOfLivingAllowance  { get; set; }
        public decimal  PerformanceAllowance   { get; set; }
        public decimal  HealthAllowance        { get; set; }
        public decimal  OtherAllowances        { get; set; }
        public decimal  SalaryForEPF           { get; set; }

        // ═══════════════════════════════════════════════════════════
        // WORKING / LEAVE
        // ═══════════════════════════════════════════════════════════
        public double   WorkingDays            { get; set; }
        public double   WorkingHours           { get; set; }
        public double   LeaveDays              { get; set; }

        // ═══════════════════════════════════════════════════════════
        // OVERTIME — Normal
        // (DB: NoramlOverTimePay / NoramlOverTimePoyaHours — typo preserved)
        // ═══════════════════════════════════════════════════════════
        public decimal  NoramlOverTimePay          { get; set; }
        public double   NoramlOverTimePoyaHours    { get; set; }

        // ═══════════════════════════════════════════════════════════
        // OVERTIME — Weekly Day Off
        // ═══════════════════════════════════════════════════════════
        public decimal  WeeklyDayOffOverTimePay    { get; set; }
        public double   WeeklyDayOffOverTimeHours  { get; set; }

        // ═══════════════════════════════════════════════════════════
        // OVERTIME — Weekly Half Day Off
        // ═══════════════════════════════════════════════════════════
        public decimal  WeeklyHalfDayOverTimePay   { get; set; }
        public double   WeeklyHalfDayOverTimeHours { get; set; }

        // ═══════════════════════════════════════════════════════════
        // OVERTIME — Poya Day
        // ═══════════════════════════════════════════════════════════
        public decimal  PoyaDayOverTimePay         { get; set; }
        public double   PoyaDayOverTimeHours       { get; set; }

        // ═══════════════════════════════════════════════════════════
        // OVERTIME — Bank Holiday
        // ═══════════════════════════════════════════════════════════
        public decimal  BankHolidayOverTimePay     { get; set; }
        public double   BankHolidayOverTimeHours   { get; set; }

        // ═══════════════════════════════════════════════════════════
        // OVERTIME — Mercantile Holiday
        // ═══════════════════════════════════════════════════════════
        public decimal  MercantileHolidayOverTimePay   { get; set; }
        public double   MercantileHolidayOverTimeHours { get; set; }

        // ═══════════════════════════════════════════════════════════
        // OVERTIME — Extra / Sunday  (nullable in DB)
        // ═══════════════════════════════════════════════════════════
        public decimal? ExtraOverTimePay           { get; set; }
        public double?  ExtraOverTimeHours         { get; set; }

        // ═══════════════════════════════════════════════════════════
        // DEDUCTIONS
        // ═══════════════════════════════════════════════════════════
        public decimal  PayCutDeduction    { get; set; }
        public double   PayCutHours        { get; set; }
        public decimal  NoPayDeduction     { get; set; }
        public double   NoPayDays          { get; set; }   // maps from DB NoPayCount
        public decimal  Epf8               { get; set; }
        public decimal  PayeeDeduction     { get; set; }
        public decimal  CashShortDeduction { get; set; }
        public decimal  WelfareDeduction   { get; set; }
        public decimal  Loan               { get; set; }
        public decimal  SalaryAdvance      { get; set; }
        public decimal  OtherDeduction     { get; set; }

        // ═══════════════════════════════════════════════════════════
        // TOTALS
        // ═══════════════════════════════════════════════════════════
        public decimal  TotalEarning       { get; set; }
        public decimal  TotalDeduction     { get; set; }
        public decimal  GrossSalary        { get; set; }
        public decimal  NetPay             { get; set; }
        public decimal  Epf12              { get; set; }
        public decimal  Etf3               { get; set; }

        // ═══════════════════════════════════════════════════════════
        // PRINT-ONLY EXTRAS (not from DB — populated by frontend/controller)
        // ═══════════════════════════════════════════════════════════
        public string?  CompanyLogo        { get; set; }   // e.g. "/images/logo/1.png"
        public string?  SignaturePath      { get; set; }   // e.g. "/images/signatures/1.png"
        public bool     Locked             { get; set; }   // period lock state
    }
}