using System;

namespace TRIVORA_API.Models
{
    public class EmployeeStatusHistory
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string FieldName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangedDate { get; set; }
    }
}