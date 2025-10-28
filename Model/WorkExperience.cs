using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
public class WorkExperience
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string Position { get; set; }
    
    [Required]
    public string Company { get; set; }
    
    public int FromYear { get; set; }
    public string FromMonth { get; set; }
    public string ToYear { get; set; } // Change from int to string
    public string ToMonth { get; set; }
    public string Description { get; set; }
    
    public DateTime CreatedDate { get; set; }
    public int EmployeeId { get; set; }
    
    [ForeignKey("EmployeeId")]
    public virtual Employee Employee { get; set; }
}
}