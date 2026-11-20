// TRIVORA_API.Models.ExcelImport
public class ExcelImport
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int SuccessfulRows { get; set; }
    public int FailedRows { get; set; }
    public string MappingData { get; set; } = string.Empty;
    public string RowData { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ErrorMessage { get; set; }
}