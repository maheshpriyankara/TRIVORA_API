namespace TRIVORA_API.Models
{
    public class PayrollPeriodDto
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int Year { get; set; }
        public string Month { get; set; } = string.Empty;
        public DateTime PayrollStartDate { get; set; }
        public bool Processing { get; set; }
        public bool Locked { get; set; }
    }
}