using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
    [Table("ShiftsTimesheet")]
    public class ShiftTimesheet
    {
        [Required]
        public int EmployeeId { get; set; }

        [Required]
        [Column("Date")]
        public DateTime Date { get; set; }

        public int? ShiftBlockIdFirstShift { get; set; }
        public int? ShiftBlockIdSecondShift { get; set; }
    }
}