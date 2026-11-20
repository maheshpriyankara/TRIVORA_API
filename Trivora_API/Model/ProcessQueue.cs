namespace TRIVORA_API.Models
{
    public class ProcessQueue
    {
        public int id { get; set; }
        public int emp_systemID { get; set; }
        public string emp_empID { get; set; } = string.Empty;
        public string requset_from { get; set; } = string.Empty;  // ⚠️ typo as in DB
        public DateTime request_date { get; set; }
        public string? request_by { get; set; }
        public string? request_ip { get; set; }
        public int process_status { get; set; }
        public bool processing { get; set; }
        public bool process_end { get; set; }
        public DateTime processStart_date { get; set; }
        public DateTime processEnd_stageOne_date { get; set; }
        public DateTime processEnd_stageTwo_date { get; set; }
        public string period_ { get; set; } = string.Empty;
    }
}