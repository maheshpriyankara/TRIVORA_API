using System.ComponentModel.DataAnnotations;

[System.ComponentModel.DataAnnotations.Schema.Table("Calendar")]
public class Calendar
{
    [Key] public int Id { get; set; }
    public int CompanyId { get; set; }
    public DateTime Date { get; set; }
    public string? DateName { get; set; }
    public bool IsPayDate { get; set; }
    public bool IsPoya { get; set; }
    public bool IsBankHoliday { get; set; }
    public bool IsMercantile { get; set; }
}