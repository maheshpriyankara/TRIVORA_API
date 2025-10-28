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

        // Audit tables
        public DbSet<BasicSalaryHistory> BasicSalaryHistory { get; set; }
        public DbSet<EmployeeStatusHistory> EmployeeStatusHistory { get; set; }
        public DbSet<BankDetailsHistory> BankDetailsHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
        // ... other salary properties ...

        // **FIX: Properly configure relationships with explicit foreign keys**
        entity.HasMany(e => e.Qualifications)
            .WithOne(q => q.Employee) // This line is crucial
            .HasForeignKey(q => q.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(e => e.WorkExperiences)
            .WithOne(we => we.Employee) // This line is crucial
            .HasForeignKey(we => we.EmployeeId)
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

            // Qualifications - Add explicit configuration
            modelBuilder.Entity<Qualification>(entity =>
            {
                entity.HasKey(e => e.Id);

                // **FIX: Explicitly define the relationship and foreign key**
                entity.HasOne(q => q.Employee)
                    .WithMany(e => e.Qualifications)
                    .HasForeignKey(q => q.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
            });

            // Work Experiences - Add explicit configuration
            modelBuilder.Entity<WorkExperience>(entity =>
            {
                entity.HasKey(e => e.Id);

                // **FIX: Explicitly define the relationship and foreign key**
                entity.HasOne(we => we.Employee)
                    .WithMany(e => e.WorkExperiences)
                    .HasForeignKey(we => we.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
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

                // Relationship with Employee
                entity.HasOne<Employee>()
                    .WithMany()
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict); // Don't cascade delete audit records
            });

            // Employee Status History
            modelBuilder.Entity<EmployeeStatusHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ChangedDate).HasDefaultValueSql("GETDATE()");

                // Configure field sizes
                entity.Property(e => e.FieldName).HasMaxLength(100);
                entity.Property(e => e.OldValue).HasMaxLength(500);
                entity.Property(e => e.NewValue).HasMaxLength(500);
                entity.Property(e => e.ChangedBy).HasMaxLength(100);

                // Relationship with Employee
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

                // Configure field sizes
                entity.Property(e => e.SalaryPayType).HasMaxLength(20);
                entity.Property(e => e.AccountNumber).HasMaxLength(20);
                entity.Property(e => e.BankAccountName).HasMaxLength(100);
                entity.Property(e => e.BankCode).HasMaxLength(10);
                entity.Property(e => e.BankName).HasMaxLength(100);
                entity.Property(e => e.BranchCode).HasMaxLength(10);
                entity.Property(e => e.BranchName).HasMaxLength(100);
                entity.Property(e => e.ChangedBy).HasMaxLength(100);

                // Relationship with Employee
                entity.HasOne<Employee>()
                    .WithMany()
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}