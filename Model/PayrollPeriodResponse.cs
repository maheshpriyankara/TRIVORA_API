namespace TRIVORA_API.Models
{
public class PayrollPeriodResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<PayrollPeriodDto> Data { get; set; } = new List<PayrollPeriodDto>();
    }
}