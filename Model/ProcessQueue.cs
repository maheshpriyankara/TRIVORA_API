using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("process_queue")]
public class ProcessQueue
{
    [Key]
    public int id { get; set; }
    public int emp_systemID { get; set; }
    public string emp_empID { get; set; }
    public string requset_from { get; set; }
    public DateTime request_date { get; set; }
    public string request_by { get; set; }
    public string request_ip { get; set; }
    public bool process_status { get; set; }   // false = pending, true = completed
    public bool processing { get; set; }
    public bool process_end { get; set; }
    public DateTime processStart_date { get; set; }
    public DateTime processEnd_stageOne_date { get; set; }
    public DateTime processEnd_stageTwo_date { get; set; }
    public string period_ { get; set; }
}