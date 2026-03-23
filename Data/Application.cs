using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
using TRIVORA_API.Controllers;
using TRIVORA_API.Models;

namespace TRIVORA_API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Employee and related tables
        public DbSet<Employee> Employees { get; set; }
        public DbSet<CompanyDesignation> Company_Designations { get; set; }
        public DbSet<CompanyDepartments> Company_Departments { get; set; }
        public DbSet<ShiftBlock> ShiftBlocks { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Qualification> Qualifications { get; set; }
        public DbSet<WorkExperience> WorkExperiences { get; set; }
        public DbSet<AdminUser> Admin_Users { get; set; }
        // New tables for custom approvers
        public DbSet<LeaveCustomApprover> LeaveCustomApprovers { get; set; }
        public DbSet<FinanceCustomApprover> FinanceCustomApprovers { get; set; }

        // Audit tables
        public DbSet<BasicSalaryHistory> BasicSalaryHistory { get; set; }
        public DbSet<EmployeeStatusHistory> EmployeeStatusHistory { get; set; }
        public DbSet<BankDetailsHistory> BankDetailsHistory { get; set; }

        // Add Deductions table
        public DbSet<Deduction> Deductions { get; set; }
        public DbSet<TimeSheet> TimeSheets { get; set; }

         public DbSet<Leave> Leave { get; set; }
          public DbSet<OtherLeaves> OtherLeaves { get; set; }
        public DbSet<Allowance> Allowances { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Leave configuration  
        // Leave configuration  
        modelBuilder.Entity<Leave>(entity =>
        {
            entity.ToTable("Leave");
            entity.HasKey(e => e.Id);

            // Configure default values
            entity.Property(e => e.IsHalfDay).HasDefaultValue(false);
            entity.Property(e => e.Status).HasDefaultValue("Pending");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
            
            // Configure string lengths
            entity.Property(e => e.LeaveType).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Remarks).HasMaxLength(500);
            
            // Configure the new properties
            entity.Property(e => e.CancelledBy);
            entity.Property(e => e.CancelledDate);
        });

        // OtherLeaves configuration
        modelBuilder.Entity<OtherLeaves>(entity =>
        {
            entity.ToTable("OtherLeaves");
            entity.HasKey(e => e.Id);

            // Configure default values
            entity.Property(e => e.Status).HasDefaultValue("Pending");
            entity.Property(e => e.ApprovedDate).HasDefaultValueSql("GETDATE()");
        });
            // SIMPLE Deductions table configuration - just map to table
            modelBuilder.Entity<Deduction>().ToTable("Deductions");

            // Company Designations
            modelBuilder.Entity<CompanyDesignation>()
                .ToTable("Company_Designations");

            modelBuilder.Entity<CompanyDesignation>()
                .HasIndex(d => new { d.CompanyId, d.Designation })
                .IsUnique();

            // Company Departments
            modelBuilder.Entity<CompanyDepartments>()
                .ToTable("Company_Departments");

            modelBuilder.Entity<CompanyDepartments>()
                .HasIndex(d => new { d.CompanyId, d.DepartmentName })
                .IsUnique();

            

            modelBuilder.Entity<TimeSheet>(entity =>
            {
                entity.ToTable("TimeSheet");
                entity.HasKey(e => e.id);

                // Configure default values to match your database schema
                entity.Property(e => e.LateHours).HasDefaultValue(TimeSpan.Zero);
                entity.Property(e => e.OtHoursNormal).HasDefaultValue(TimeSpan.Zero);
                entity.Property(e => e.OtHoursDouble).HasDefaultValue(TimeSpan.Zero);
                entity.Property(e => e.OtHoursExtra).HasDefaultValue(TimeSpan.Zero);
                entity.Property(e => e.WorkHours).HasDefaultValue(TimeSpan.Zero);
                entity.Property(e => e.DayType).HasDefaultValue("Normal");
                entity.Property(e => e.PayType).HasDefaultValue("Regular");
            });
            // Shift Blocks
            modelBuilder.Entity<ShiftBlock>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => new { e.CompanyId, e.IsActive });
                entity.HasIndex(e => e.ShiftName);

                // Configure decimal precision
                entity.Property(e => e.DayOffRate).HasPrecision(18, 2);
                entity.Property(e => e.LateHoursThreshold).HasPrecision(5, 2);
                entity.Property(e => e.AttendanceAllowancePercent).HasPrecision(5, 2);
                entity.Property(e => e.AttendanceAllowancePercent2).HasPrecision(5, 2);
                entity.Property(e => e.AttendanceAllowancePercent3).HasPrecision(5, 2);
                entity.Property(e => e.AttendanceAllowancePercent4).HasPrecision(5, 2);
                entity.Property(e => e.AttendanceAllowancePercent5).HasPrecision(5, 2);
                entity.Property(e => e.LateDeductRate).HasPrecision(18, 2);
                entity.Property(e => e.OvertimeRate).HasPrecision(18, 2);
                entity.Property(e => e.OtMinimumHours).HasPrecision(5, 2);

                // Configure default values
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.IsAttendance).HasDefaultValue(false);
                entity.Property(e => e.IsAttendanceAllowance).HasDefaultValue(false);
                entity.Property(e => e.IsLateDeduction).HasDefaultValue(false);
                entity.Property(e => e.IsOvertimePay).HasDefaultValue(false);
                entity.Property(e => e.ShiftType).HasDefaultValue("Single");
            });

            // Company
            modelBuilder.Entity<Company>(entity =>
            {
                entity.ToTable("Company");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.CompanyName).HasMaxLength(50);
                entity.Property(e => e.CompanyAddress).HasMaxLength(250);
            });

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Configure decimal precision for salary fields
                entity.Property(e => e.BasicSalary).HasPrecision(18, 2);
                entity.Property(e => e.BudgetaryAllowance).HasPrecision(18, 2);

                // Configure new fields
                entity.Property(e => e.OccupationNo);
                entity.Property(e => e.OccupationGrade);
                entity.Property(e => e.RoleType).HasDefaultValue("Standard Employee");
                entity.Property(e => e.LeaveApproval).HasDefaultValue("Super Admin");
                entity.Property(e => e.FinanceApproval).HasDefaultValue("Super Admin");

                // Configure relationships
                entity.HasMany(e => e.Qualifications)
                    .WithOne(q => q.Employee)
                    .HasForeignKey(q => q.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.WorkExperiences)
                    .WithOne(we => we.Employee)
                    .HasForeignKey(we => we.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.LeaveCustomApprovers)
                    .WithOne(l => l.Employee)
                    .HasForeignKey(l => l.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.FinanceCustomApprovers)
                    .WithOne(f => f.Employee)
                    .HasForeignKey(f => f.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure default values
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.SalaryPayType).HasDefaultValue("CashPay");
                entity.Property(e => e.EPFPay).HasDefaultValue(false);
                entity.Property(e => e.Probation).HasDefaultValue(false);
                entity.Property(e => e.Block).HasDefaultValue(false);
                entity.Property(e => e.Resigned).HasDefaultValue(false);
                entity.Property(e => e.BlockAttendance).HasDefaultValue(false);
            });

            // Qualifications
            modelBuilder.Entity<Qualification>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(q => q.Employee)
                    .WithMany(e => e.Qualifications)
                    .HasForeignKey(q => q.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
            });

            // Work Experiences
            modelBuilder.Entity<WorkExperience>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(we => we.Employee)
                    .WithMany(e => e.WorkExperiences)
                    .HasForeignKey(we => we.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
            });

            // Leave Custom Approvers
            modelBuilder.Entity<LeaveCustomApprover>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(l => l.Employee)
                    .WithMany(e => e.LeaveCustomApprovers)
                    .HasForeignKey(l => l.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.ApproverEmployee)
                    .WithMany()
                    .HasForeignKey(l => l.ApproverEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            // Finance Custom Approvers
            modelBuilder.Entity<FinanceCustomApprover>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(f => f.Employee)
                    .WithMany(e => e.FinanceCustomApprovers)
                    .HasForeignKey(f => f.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(f => f.ApproverEmployee)
                    .WithMany()
                    .HasForeignKey(f => f.ApproverEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            // Audit Tables Configuration
            ConfigureAuditTables(modelBuilder);
        }

        private void ConfigureAuditTables(ModelBuilder modelBuilder)
        {
            // Basic Salary History
            modelBuilder.Entity<BasicSalaryHistory>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.OldBasicSalary).HasPrecision(18, 2);
                entity.Property(e => e.NewBasicSalary).HasPrecision(18, 2);
                entity.Property(e => e.ChangedDate).HasDefaultValueSql("GETDATE()");

                entity.HasOne<Employee>()
                    .WithMany()
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Employee Status History
            modelBuilder.Entity<EmployeeStatusHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ChangedDate).HasDefaultValueSql("GETDATE()");

                entity.Property(e => e.FieldName).HasMaxLength(100);
                entity.Property(e => e.OldValue).HasMaxLength(500);
                entity.Property(e => e.NewValue).HasMaxLength(500);
                entity.Property(e => e.ChangedBy).HasMaxLength(100);

                entity.HasOne<Employee>()
                    .WithMany()
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Bank Details History
            modelBuilder.Entity<BankDetailsHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ChangedDate).HasDefaultValueSql("GETDATE()");

                entity.Property(e => e.SalaryPayType).HasMaxLength(20);
                entity.Property(e => e.AccountNumber).HasMaxLength(20);
                entity.Property(e => e.BankAccountName).HasMaxLength(100);
                entity.Property(e => e.BankCode).HasMaxLength(10);
                entity.Property(e => e.BankName).HasMaxLength(100);
                entity.Property(e => e.BranchCode).HasMaxLength(10);
                entity.Property(e => e.BranchName).HasMaxLength(100);
                entity.Property(e => e.ChangedBy).HasMaxLength(100);

                entity.HasOne<Employee>()
                    .WithMany()
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}