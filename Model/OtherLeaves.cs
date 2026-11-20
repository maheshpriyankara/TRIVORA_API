using System;
using System.ComponentModel.DataAnnotations;

namespace TRIVORA_API.Models
{
    public class OtherLeaves
    {
        [Key]
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int EmployeeId { get; set; }
        public string LeaveType { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string Remarks { get; set; }
        public string Status { get; set; } = "Pending";
        public int? ApprovedBy { get; set; }
        public DateTime? ApprovedDate { get; set; }
    }
}