using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TRIVORA_API.Models
{
    public class ExcelImport
    {
        [Key]
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public DateTime UploadDate { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public string? ErrorMessage { get; set; }
        public int TotalRows { get; set; }
        public int SuccessfulRows { get; set; }
        public int FailedRows { get; set; }
        public string? MappingData { get; set; }
        public string? RowData { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class ExcelColumnMapping
    {
        public string ExcelColumn { get; set; } = string.Empty;
        public string? DatabaseField { get; set; }
        public string? DataType { get; set; }
        public bool IsMapped { get; set; }
        public double Confidence { get; set; }
        public string? SampleData { get; set; }
        public string? SuggestedField { get; set; }
        public string? ValidationStatus { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ExcelPreviewData
{
    public List<string> Columns { get; set; } = new List<string>();
    public List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();
    public List<ExcelColumnMapping> ColumnMappings { get; set; } = new List<ExcelColumnMapping>();
    public int TotalRows { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? UploadId { get; set; }
    public bool IsRetry { get; set; } // Add this property
    public string? OriginalUploadId { get; set; } // Add this property
}

    public class ImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalProcessed { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; } 
        public List<ImportError> Errors { get; set; } = new List<ImportError>();
        public List<Dictionary<string, object>> ProcessedData { get; set; } = new List<Dictionary<string, object>>();
    }

    public class ImportError
    {
        public int RowNumber { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Dictionary<string, string> RowData { get; set; } = new Dictionary<string, string>();
    }
}