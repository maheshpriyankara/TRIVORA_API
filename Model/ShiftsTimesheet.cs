using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
    [Table("ShiftsTimesheet")]
    public class ShiftsTimesheet
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("EmployeeId")]
        public int EmployeeId { get; set; }

        [Column("ShiftBlockIdFirstShift")]
        public int? ShiftBlockIdFirstShift { get; set; }

        [Column("ShiftBlockIdSecondShift")]
        public int? ShiftBlockIdSecondShift { get; set; }

        [Required]
        [Column("Date", TypeName = "date")]
        public DateTime Date { get; set; }

        // Navigation (optional)
        [ForeignKey(nameof(EmployeeId))]
        public virtual Employee? Employee { get; set; }

        [ForeignKey(nameof(ShiftBlockIdFirstShift))]
        public virtual ShiftBlock? FirstShiftBlock { get; set; }

        [ForeignKey(nameof(ShiftBlockIdSecondShift))]
        public virtual ShiftBlock? SecondShiftBlock { get; set; }
    }
}