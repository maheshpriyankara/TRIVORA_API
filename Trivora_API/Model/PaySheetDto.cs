using System;

namespace TrivoraHrisBackend.Models
{
    public class PaySheetDto
    {
        // ── IDs ──
        public int      PaySheetId        { get; set; }
        public int      EmployeeId        { get; set; }
        public int      CompanyId         { get; set; }
        public int      DepartmentId      { get; set; }
        public int      DesignationId     { get; set; }
        public int      ShiftBlockId      { get; set; }

        // ── Payroll period ──
        public string   Period            { get; set; } = "";
        public bool     EpfPay            { get; set; }
        public string   SalaryPayType     { get; set; } = "Bank";
        public string   Nic               { get; set; } = "";

        // ── Employee (joined) ──
        public string   EmployeeName      { get; set; } = "";
        public string   EmployeeNo        { get; set; } = "";
        public string   EpfNo             { get; set; } = "";
        public string   ProfileImage      { get; set; } = "";

        // ── Company (joined) ──
        public string   CompanyName       { get; set; } = "";
        public string   CompanyAddress    { get; set; } = "";

        // ── Department / Designation (joined) ──
        public string   Department        { get; set; } = "";
        public string   Designation       { get; set; } = "";

        // ── Bank (from payroll) ──
        public string   BankName          { get; set; } = "";
        public string   BranchName        { get; set; } = "";
        public string   BankCode          { get; set; } = "";
        public string   BranchCode        { get; set; } = "";
        public string   AccountNumber     { get; set; } = "";

        // ── Earnings (Paysheet) ──
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

        // ── Working / Leave ──
        public decimal  SalaryForEPF           { get; set; }
        public decimal  WorkingDays            { get; set; }
        public decimal  WorkingHours           { get; set; }
        public decimal  LeaveDays              { get; set; }

        // ── Overtime — Normal ──
        public decimal  NoramlOverTimePay         { get; set; }
        public decimal  NoramlOverTimePoyaHours   { get; set; }   // hours

        // ── Overtime — Weekly Day Off ──
        public decimal  WeeklyDayOffOverTimePay   { get; set; }
        public decimal  WeeklyDayOffOverTimeHours { get; set; }

        // ── Overtime — Weekly Half Day Off ──
        public decimal  WeeklyHalfDayOverTimePay  { get; set; }
        public decimal  WeeklyHalfDayOverTimeHours{ get; set; }

        // ── Overtime — Poya ──
        public decimal  PoyaDayOverTimePay        { get; set; }
        public decimal  PoyaDayOverTimeHours      { get; set; }

        // ── Overtime — Bank Holiday ──
        public decimal  BankHolidayOverTimePay    { get; set; }
        public decimal  BankHolidayOverTimeHours  { get; set; }

        // ── Overtime — Mercantile ──
        public decimal  MercantileHolidayOverTimePay   { get; set; }
        public decimal  MercantileHolidayOverTimeHours { get; set; }

        // ── Overtime — Extra / Sunday ──
        public decimal? ExtraOverTimePay          { get; set; }
        public decimal? ExtraOverTimeHours        { get; set; }

        // ── Deductions ──
        public decimal  PayCutDeduction    { get; set; }
        public double   PayCutHours        { get; set; }    // ← FIXED: float in DB, not int
        public decimal  NoPayDeduction     { get; set; }
        public double   NoPayDays          { get; set; }    // ← FIXED: float in DB, not int
        public decimal  Epf8               { get; set; }
        public decimal  PayeeDeduction     { get; set; }
        public decimal  CashShortDeduction { get; set; }
        public decimal  WelfareDeduction   { get; set; }
        public decimal  Loan               { get; set; }
        public decimal  SalaryAdvance      { get; set; }
        public decimal  OtherDeduction     { get; set; }

        // ── Totals ──
        public decimal  TotalEarning       { get; set; }
        public decimal  TotalDeduction     { get; set; }
        public decimal  GrossSalary        { get; set; }
        public decimal  NetPay             { get; set; }
        public decimal  Epf12              { get; set; }
        public decimal  Etf3               { get; set; }
    }
}