using System;

namespace TRIVORA_API.Models
{
    public class BasicSalaryHistory
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public decimal? OldBasicSalary { get; set; }
        public decimal? NewBasicSalary { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangedDate { get; set; }
    }
}