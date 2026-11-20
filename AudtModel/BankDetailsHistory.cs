using System;

namespace TRIVORA_API.Models
{
    public class BankDetailsHistory
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string SalaryPayType { get; set; }
        public string AccountNumber { get; set; }
        public string BankAccountName { get; set; }
        public string BankCode { get; set; }
        public string BankName { get; set; }
        public string BranchCode { get; set; }
        public string BranchName { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangedDate { get; set; }
    }
}