
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
using TRIVORA_API.Models;

namespace TRIVORA_API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Employee> Employees { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure primary key
            modelBuilder.Entity<Employee>()
                .HasKey(e => e.EPFNo);

            // Configure indexes for better performance
            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.EmployeeNo)
                .IsUnique();

            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.NIC)
                .IsUnique();

            // Configure decimal precision for salary fields
            modelBuilder.Entity<Employee>()
                .Property(e => e.BasicSalary)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Employee>()
                .Property(e => e.BudgetaryAllowance)
                .HasColumnType("decimal(18,2)");

            // Add similar configurations for other decimal properties
        }
    }
}