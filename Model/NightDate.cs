using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
    [Table("NightDates")]
    public class NightDate
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        [Column("Date")]
        public DateTime Date { get; set; }

        public bool IsFullNight { get; set; }

        [StringLength(255)]
        public string? Remarks { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public int? CreatedBy { get; set; }
    }
}