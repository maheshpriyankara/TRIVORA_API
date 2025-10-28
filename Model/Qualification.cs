using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
   public class Qualification
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string QualificationName { get; set; }
    
    [Required]
    public string InstituteName { get; set; }
    
    public int Year { get; set; }
    public string Month { get; set; }
    public string Description { get; set; }
    
    public DateTime CreatedDate { get; set; }
    // Foreign key
    public int EmployeeId { get; set; }
    
    // Navigation property
    [ForeignKey("EmployeeId")]
    public virtual Employee Employee { get; set; }
}
}