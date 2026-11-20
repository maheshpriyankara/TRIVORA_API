namespace TrivoraHrisBackend.Models
{
    public class PaySheetEpfEtfTotalsDto
{
    public int?    FirstPeriodKey { get; set; }  // e.g. 202501
    public int?    LastPeriodKey  { get; set; }  // e.g. 202611
    public int     MonthCount     { get; set; }
    public decimal TotalEpf12     { get; set; }
    public decimal TotalEpf8      { get; set; }
    public decimal TotalEtf3      { get; set; }
}
}