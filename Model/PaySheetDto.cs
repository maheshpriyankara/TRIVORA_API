using System;

namespace TrivoraHrisBackend.Models
{
    public class PaySheetDto
    {
        public int      PaySheetId        { get; set; }
        public int      EmployeeId        { get; set; }
        public int      CompanyId         { get; set; }
        public int      DepartmentId      { get; set; }
        public int      DesignationId     { get; set; }
        public int      ShiftBlockId      { get; set; }
        public string   Period            { get; set; } = "";
        public bool     EpfPay            { get; set; }
        public string   SalaryPayType     { get; set; } = "Bank";
        public string   Nic               { get; set; } = "";

        // Employee (joined)
        public string   EmployeeName      { get; set; } = "";
        public string   EmployeeNo        { get; set; } = "";
        public string   EpfNo             { get; set; } = "";

        // Company (joined)
        public string   CompanyName       { get; set; } = "";
        public string   CompanyAddress    { get; set; } = "";

        // Department / Designation (joined)
        public string   Department        { get; set; } = "";
        public string   Designation       { get; set; } = "";

        // Bank (joined or from payroll)
        public string   BankName          { get; set; } = "";
        public string   BankBranch        { get; set; } = "";
        public string   BankCode          { get; set; } = "";
        public string   BranchCode        { get; set; } = "";
        public string   AccountNumber     { get; set; } = "";

        // Earnings (from payroll table)
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

        // Calculated (set by service — you fill these from your existing calc logic)
        public decimal  SalaryForEPF             { get; set; }
        public decimal  OverTimePay              { get; set; }
        public int      OverTimeHours            { get; set; }   // SECONDS
        public decimal  OverTimePoyaPay          { get; set; }
        public int      OverTimePoyaHours        { get; set; }   // SECONDS
        public decimal  OverTimeBankHolidayPay   { get; set; }
        public int      OverTimeBankHolidayHours { get; set; }   // SECONDS
        public decimal  OverTimeSundayPay        { get; set; }
        public int      OverTimeSundayHours      { get; set; }   // SECONDS

        public decimal  PayCutDeduction    { get; set; }
        public int      PayCutHours        { get; set; }         // SECONDS
        public decimal  NoPayDeduction     { get; set; }
        public decimal  Epf8               { get; set; }
        public decimal  PayeeDeduction     { get; set; }
        public decimal  CashShortDeduction { get; set; }
        public decimal  WelfareDeduction   { get; set; }
        public decimal  Loan               { get; set; }
        public decimal  SalaryAdvance      { get; set; }
        public decimal  OtherDeduction     { get; set; }

        public decimal  TotalEarning       { get; set; }
        public decimal  TotalDeduction     { get; set; }
        public decimal  GrossSalary        { get; set; }
        public decimal  NetPay             { get; set; }
        public decimal  Epf12              { get; set; }
        public decimal  Etf3               { get; set; }

        public int      WorkingDays        { get; set; }
        public int      NoPayDays          { get; set; }
    }
}