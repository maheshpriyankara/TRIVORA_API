using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TRIVORA_API.Data;
using TRIVORA_API.Models;
using TRIVORA_API.Services;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExcelImportController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ExcelImportController> _logger;

private readonly ICompanySettingsService _companySettingsService;
        private static readonly Dictionary<string, ExcelPreviewData> _importCache = new();
        private static readonly Dictionary<string, ImportJobStatus> _jobStatusCache = new();

        public ExcelImportController(
            ApplicationDbContext context,
            ILogger<ExcelImportController> logger,ICompanySettingsService companySettingsService)
        {
            _context = context;
            _logger = logger;
            _companySettingsService = companySettingsService;
        }

        #region Public Endpoints

        [HttpPost("upload")]
        public async Task<ActionResult<ApiResponse<ExcelPreviewData>>> UploadExcel(IFormFile file)
        {
            try
            {
                _logger.LogInformation("=== UPLOAD EXCEL STARTED ===");
                
                if (file == null || file.Length == 0)
                    return BadRequest(new ApiResponse<ExcelPreviewData>
                    {
                        Success = false,
                        Message = "No file uploaded"
                    });

                var extension = Path.GetExtension(file.FileName).ToLower();
                if (extension != ".xlsx" && extension != ".xls")
                    return BadRequest(new ApiResponse<ExcelPreviewData>
                    {
                        Success = false,
                        Message = "Invalid file format. Please upload .xlsx or .xls files."
                    });

                ExcelPreviewData editedData = null;
                bool hasEditedData = false;

                if (Request.Form.ContainsKey("editedData"))
                {
                    var editedDataJson = Request.Form["editedData"].FirstOrDefault();
                    _logger.LogInformation($"Received editedData JSON length: {editedDataJson?.Length ?? 0}");
                    
                    if (!string.IsNullOrEmpty(editedDataJson))
                    {
                        try
                        {
                            var jsonObject = JsonSerializer.Deserialize<Dictionary<string, object>>(editedDataJson);
                            
                            if (jsonObject != null && jsonObject.ContainsKey("rows"))
                            {
                                var rowsJson = jsonObject["rows"].ToString();
                                var rows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(rowsJson);
                                
                                if (rows != null && rows.Count > 0)
                                {
                                    editedData = new ExcelPreviewData();
                                    editedData.Rows = rows;
                                    
                                    if (jsonObject.ContainsKey("columns"))
                                    {
                                        var columnsJson = jsonObject["columns"].ToString();
                                        editedData.Columns = JsonSerializer.Deserialize<List<string>>(columnsJson) ?? new List<string>();
                                    }
                                    else
                                    {
                                        if (rows.Count > 0)
                                        {
                                            editedData.Columns = rows[0].Keys.ToList();
                                        }
                                    }
                                    
                                    hasEditedData = true;
                                    _logger.LogInformation($"✅ Successfully parsed edited data with {rows.Count} rows");
                                }
                            }
                            else
                            {
                                try
                                {
                                    var direct = JsonSerializer.Deserialize<ExcelPreviewData>(editedDataJson);
                                    if (direct != null && direct.Rows != null && direct.Rows.Count > 0)
                                    {
                                        editedData = direct;
                                        hasEditedData = true;
                                        _logger.LogInformation($"✅ Deserialized directly with {direct.Rows.Count} rows");
                                    }
                                }
                                catch (Exception ex2)
                                {
                                    _logger.LogWarning($"Direct deserialization failed: {ex2.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"❌ Failed to parse edited data: {ex.Message}");
                        }
                    }
                }

                if (!hasEditedData || editedData == null)
                {
                    _logger.LogInformation("No edited data found, reading from Excel file");
                    
                    using var stream = file.OpenReadStream();
                    using var workbook = new XLWorkbook(stream);

                    var worksheet = workbook.Worksheet(1);
                    if (worksheet == null)
                        return BadRequest(new ApiResponse<ExcelPreviewData>
                        {
                            Success = false,
                            Message = "The Excel file is empty or invalid"
                        });

                    var range = worksheet.RangeUsed();
                    if (range == null)
                        return BadRequest(new ApiResponse<ExcelPreviewData>
                        {
                            Success = false,
                            Message = "The Excel file is empty"
                        });

                    var rows = range.RowsUsed().ToList();
                    if (rows.Count == 0)
                        return BadRequest(new ApiResponse<ExcelPreviewData>
                        {
                            Success = false,
                            Message = "No data found in the Excel file"
                        });

                    var firstRow = rows.First();
                    var colCount = range.ColumnCount();
                    var headers = new List<string>();

                    _logger.LogInformation("=== Reading Excel Headers ===");
                    for (int col = 1; col <= colCount; col++)
                    {
                        var cell = firstRow.Cell(col);
                        var header = cell.GetString().Trim();
                        if (!string.IsNullOrEmpty(header))
                        {
                            headers.Add(header);
                            _logger.LogInformation($"Header {col}: '{header}'");
                        }
                    }

                    if (headers.Count == 0)
                        return BadRequest(new ApiResponse<ExcelPreviewData>
                        {
                            Success = false,
                            Message = "No column headers found in the Excel file"
                        });

                    var allRows = new List<Dictionary<string, object>>();
                    _logger.LogInformation("=== Reading Excel Data Rows ===");
                    
                    for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
                    {
                        var row = rows[rowIndex];
                        var rowData = new Dictionary<string, object>();

                        for (int col = 0; col < headers.Count && col < colCount; col++)
                        {
                            var colIndex = col + 1;
                            var cell = row.Cell(colIndex);
                            var value = cell.Value;

                            string stringValue = "";
                            try
                            {
                                if (value.Type == XLDataType.DateTime)
                                {
                                    stringValue = value.GetDateTime().ToString("yyyy-MM-dd");
                                }
                                else if (value.Type == XLDataType.Number)
                                {
                                    var dbl = value.GetNumber();
                                    if (Math.Abs(dbl - Math.Round(dbl)) < 0.000001)
                                    {
                                        stringValue = Convert.ToInt64(dbl).ToString();
                                    }
                                    else
                                    {
                                        stringValue = dbl.ToString("F2");
                                    }
                                }
                                else if (value.Type == XLDataType.Boolean)
                                {
                                    stringValue = value.GetBoolean() ? "Yes" : "No";
                                }
                                else if (value.Type == XLDataType.Text)
                                {
                                    stringValue = value.ToString();
                                }
                                else
                                {
                                    stringValue = value.ToString() ?? "";
                                }
                            }
                            catch
                            {
                                stringValue = value.ToString() ?? "";
                            }

                            rowData[headers[col]] = stringValue;
                        }

                        if (rowData.Values.Any(v => !string.IsNullOrEmpty(v?.ToString())))
                        {
                            allRows.Add(rowData);
                            _logger.LogInformation($"Row {rowIndex}: {JsonSerializer.Serialize(rowData)}");
                        }
                    }

                    if (allRows.Count == 0)
                        return BadRequest(new ApiResponse<ExcelPreviewData>
                        {
                            Success = false,
                            Message = "No valid data rows found in the Excel file"
                        });

                    var columnMappings = headers.Select(h => new ExcelColumnMapping
                    {
                        ExcelColumn = h,
                        DatabaseField = null,
                        IsMapped = false,
                        Confidence = 0,
                        DataType = "string"
                    }).ToList();

                    editedData = new ExcelPreviewData
                    {
                        Columns = headers,
                        Rows = allRows,
                        ColumnMappings = columnMappings,
                        TotalRows = allRows.Count,
                        FileName = file.FileName
                    };
                }
                else
                {
                    _logger.LogInformation($"✅ Using edited data with {editedData.Rows.Count} rows");
                    
                    if (editedData.ColumnMappings == null || editedData.ColumnMappings.Count == 0)
                    {
                        editedData.ColumnMappings = editedData.Columns.Select(h => new ExcelColumnMapping
                        {
                            ExcelColumn = h,
                            DatabaseField = null,
                            IsMapped = false,
                            Confidence = 0,
                            DataType = "string"
                        }).ToList();
                    }
                    
                    editedData.FileName = file.FileName;
                    editedData.TotalRows = editedData.Rows.Count;
                }

                var uploadId = Guid.NewGuid().ToString();
                editedData.UploadId = uploadId;

                lock (_importCache)
                {
                    _logger.LogInformation($"Storing preview data with upload ID: {uploadId}");
                    _logger.LogInformation($"Rows count: {editedData.Rows.Count}");
                    _logger.LogInformation($"Columns count: {editedData.Columns.Count}");
                    
                    var oldKeys = _importCache.Keys.ToList();
                    _logger.LogInformation($"Clearing {oldKeys.Count} old cache entries");
                    
                    foreach (var key in oldKeys)
                    {
                        _logger.LogInformation($"Removing old cache entry: {key}");
                        _importCache.Remove(key);
                    }
                    
                    _importCache[uploadId] = editedData;
                    _logger.LogInformation($"✅ Cache updated with new upload ID: {uploadId}");
                }

                var responseData = new ExcelPreviewData
                {
                    UploadId = uploadId,
                    Columns = editedData.Columns,
                    Rows = editedData.Rows,
                    TotalRows = editedData.Rows.Count,
                    FileName = editedData.FileName,
                    ColumnMappings = editedData.ColumnMappings
                };

                var message = hasEditedData 
                    ? $"Excel file processed with {editedData.Rows.Count} edited rows." 
                    : $"Excel file processed successfully. Found {editedData.Rows.Count} rows and {editedData.Columns.Count} columns.";

                _logger.LogInformation($"=== UPLOAD EXCEL COMPLETED ===");
                _logger.LogInformation($"UploadId: {uploadId}");
                _logger.LogInformation($"Rows: {editedData.Rows.Count}");
                _logger.LogInformation($"Columns: {editedData.Columns.Count}");
                _logger.LogInformation($"Has Edited Data: {hasEditedData}");

                return Ok(new ApiResponse<ExcelPreviewData>
                {
                    Success = true,
                    Message = message,
                    Data = responseData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading Excel file");
                return StatusCode(500, new ApiResponse<ExcelPreviewData>
                {
                    Success = false,
                    Message = $"Error processing Excel file: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Start import as a background job with SignalR progress updates
        /// </summary>
        
        /// <summary>
        /// Get import job status (for polling fallback)
        /// </summary>
        [HttpGet("job-status/{jobId}")]
        public ActionResult<ApiResponse<ImportJobStatus>> GetJobStatus(string jobId)
        {
            try
            {
                lock (_jobStatusCache)
                {
                    if (_jobStatusCache.TryGetValue(jobId, out var status))
                    {
                        return Ok(new ApiResponse<ImportJobStatus>
                        {
                            Success = true,
                            Data = status
                        });
                    }
                }

                return NotFound(new ApiResponse<ImportJobStatus>
                {
                    Success = false,
                    Message = "Job not found"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ImportJobStatus>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("confirm-mapping")]
        public async Task<ActionResult<ApiResponse<ImportResult>>> ConfirmMapping([FromBody] ConfirmMappingRequest request)
        {
            try
            {
                _logger.LogInformation("=== CONFIRM MAPPING STARTED ===");
                _logger.LogInformation($"UploadId: {request.UploadId}");
                _logger.LogInformation($"CompanyId: {request.CompanyId}");
                _logger.LogInformation($"OverrideDuplicates: {request.OverrideDuplicates}");
                _logger.LogInformation($"Mappings: {JsonSerializer.Serialize(request.Mappings)}");

                if (string.IsNullOrEmpty(request.UploadId))
                    return BadRequest(new ApiResponse<ImportResult>
                    {
                        Success = false,
                        Message = "Upload ID is required"
                    });

                ExcelPreviewData previewData;
                lock (_importCache)
                {
                    if (!_importCache.TryGetValue(request.UploadId, out previewData))
                        return BadRequest(new ApiResponse<ImportResult>
                        {
                            Success = false,
                            Message = "Upload session not found. Please upload the file again."
                        });
                }

                _logger.LogInformation($"Preview data found. Columns: {string.Join(", ", previewData.Columns)}");

                var rowsToImport = request.Rows;
                
                if (rowsToImport == null || rowsToImport.Count == 0)
                {
                    _logger.LogWarning("No rows in request, falling back to cached rows");
                    rowsToImport = previewData.Rows;
                }
                else
                {
                    _logger.LogInformation($"Using {rowsToImport.Count} rows from request (edited data)");
                }

                var activeMappings = new List<ExcelColumnMapping>();
                foreach (var mapping in previewData.ColumnMappings)
                {
                    _logger.LogInformation($"Processing mapping for Excel column: '{mapping.ExcelColumn}'");
                    if (request.Mappings.TryGetValue(mapping.ExcelColumn, out var selectedField))
                    {
                        _logger.LogInformation($"  Found mapping: '{mapping.ExcelColumn}' -> '{selectedField}'");
                        if (!string.IsNullOrEmpty(selectedField))
                        {
                            mapping.DatabaseField = selectedField;
                            mapping.IsMapped = true;
                            mapping.Confidence = 1.0;
                            activeMappings.Add(mapping);
                        }
                        else
                        {
                            mapping.IsMapped = false;
                            mapping.DatabaseField = null;
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"  No mapping found for '{mapping.ExcelColumn}'");
                    }
                }

                _logger.LogInformation("=== FINAL MAPPINGS ===");
                foreach (var mapping in activeMappings)
                {
                    _logger.LogInformation($"  {mapping.ExcelColumn} -> {mapping.DatabaseField}");
                }

                var importResult = await ImportExcelData(
                    rowsToImport,
                    previewData.Columns,
                    activeMappings,
                    request.CompanyId ?? 0, 
                    request.OverrideDuplicates);

                var importRecord = new ExcelImport
                {
                    FileName = previewData.FileName,
                    UploadDate = DateTime.UtcNow,
                    UploadedBy = User.Identity?.Name ?? "System",
                    Status = importResult.Success ? "Completed" : "CompletedWithErrors",
                    TotalRows = previewData.TotalRows,
                    SuccessfulRows = importResult.Successful,
                    FailedRows = importResult.Failed,
                    MappingData = JsonSerializer.Serialize(activeMappings),
                    RowData = JsonSerializer.Serialize(importResult.ProcessedData),
                    CreatedDate = DateTime.UtcNow,
                    ErrorMessage = importResult.Success ? null : string.Join("; ", importResult.Errors.Select(e => e.ErrorMessage))
                };

                _context.ExcelImports.Add(importRecord);
                await _context.SaveChangesAsync();

                lock (_importCache)
                {
                    _importCache.Remove(request.UploadId);
                }

                return Ok(new ApiResponse<ImportResult>
                {
                    Success = importResult.Success,
                    Message = importResult.Success
                        ? $"Successfully imported {importResult.Successful} employees"
                        : $"Import completed with {importResult.Failed} errors",
                    Data = importResult
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming mapping and importing data");
                return StatusCode(500, new ApiResponse<ImportResult>
                {
                    Success = false,
                    Message = $"Error importing data: {ex.Message}"
                });
            }
        }

        #endregion

        #region Private Methods

        

        private async Task<ImportResult> ImportExcelData(
    List<Dictionary<string, object>> rows,
    List<string> columns,
    List<ExcelColumnMapping> activeMappings,
    int providedCompanyId,
    bool overrideDuplicates)
{
    var result = new ImportResult
    {
        Success = true,
        TotalProcessed = rows.Count,
        Errors = new List<ImportError>(),
        ProcessedData = new List<Dictionary<string, object>>(),
        Skipped = 0,
        Successful = 0,
        Failed = 0
    };

    var companyId = providedCompanyId > 0 ? providedCompanyId : await GetDefaultCompanyId();
    _logger.LogInformation($"Using CompanyId: {companyId}");

    _logger.LogInformation($"Processing {activeMappings.Count} mapped columns");
    _logger.LogInformation($"Processing {rows.Count} rows");

    int rowNumber = 0;
    foreach (var row in rows)
    {
        rowNumber++;
        _logger.LogInformation($"=== PROCESSING ROW {rowNumber} ===");
        
        try
        {
            LogRowData(row, $"Row {rowNumber}");

            var epfNo = GetStringValue(row, "EPFNo");
            var nic = GetStringValue(row, "NIC");
            
            _logger.LogInformation($"Row {rowNumber}: EPFNo = '{epfNo}', NIC = '{nic}'");

            var designationId = await ResolveOrCreateDesignation(row, companyId);
            var departmentId = await ResolveOrCreateDepartment(row, companyId);
            var shiftBlockId = await ResolveOrCreateShiftBlock(row, companyId);

            if (designationId == null)
            {
                _logger.LogWarning($"Row {rowNumber}: Designation could not be resolved, using default");
                designationId = await GetDefaultDesignationId(companyId);
            }

            if (departmentId == null)
            {
                _logger.LogWarning($"Row {rowNumber}: Department could not be resolved, using default");
                departmentId = await GetDefaultDepartmentId(companyId);
            }

            if (shiftBlockId == null)
            {
                _logger.LogWarning($"Row {rowNumber}: Shift Block could not be resolved, using default");
                shiftBlockId = await GetDefaultShiftBlockId(companyId);
            }

            row["_ResolvedDesignationId"] = designationId;
            row["_ResolvedDepartmentId"] = departmentId;
            row["_ResolvedShiftBlockId"] = shiftBlockId;

            _logger.LogInformation($"Row {rowNumber}: Resolved DesignationId={designationId}, DepartmentId={departmentId}, ShiftBlockId={shiftBlockId}");

            Employee existingEmployee = null;
            List<string> searchCriteria = new List<string>();
            
            if (!string.IsNullOrEmpty(epfNo))
{
    // Clean the EPF string
    var cleanEpfStr = System.Text.RegularExpressions.Regex.Replace(epfNo, @"[^0-9]", "");
    cleanEpfStr = cleanEpfStr.TrimStart('0');
    if (string.IsNullOrEmpty(cleanEpfStr)) cleanEpfStr = "0";

    // ✅ Parse to int for comparison (EPFNo is now int in model)
    if (int.TryParse(cleanEpfStr, out int cleanEpfInt))
    {
        existingEmployee = await _context.Employees
            .FirstOrDefaultAsync(e => e.EPFNo == cleanEpfInt && e.CompanyId == companyId);

        searchCriteria.Add($"EPFNo: {cleanEpfInt}");
    }

    if (existingEmployee == null && cleanEpfStr.Length < 10)
    {
        var paddedEpfStr = cleanEpfStr.PadLeft(3, '0');
        if (paddedEpfStr != cleanEpfStr && int.TryParse(paddedEpfStr, out int paddedEpfInt))
        {
            existingEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EPFNo == paddedEpfInt && e.CompanyId == companyId);

            if (existingEmployee != null)
                searchCriteria.Add($"EPFNo (padded): {paddedEpfInt}");
        }
    }
}
            
            if (existingEmployee == null && !string.IsNullOrEmpty(nic))
            {
                var cleanedNic = nic.Trim().ToUpper();
                existingEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.NIC == cleanedNic && e.CompanyId == companyId);
                searchCriteria.Add($"NIC: {nic}");
            }

            if (existingEmployee != null)
            {
                _logger.LogInformation($"✅ Found existing employee: ID={existingEmployee.Id}, EPFNo='{existingEmployee.EPFNo}', NIC='{existingEmployee.NIC}'");
                
                if (overrideDuplicates)
                {
                    _logger.LogInformation($"Updating existing employee: {existingEmployee.Id}");
                    
                    // ✅ NEW: Capture old data BEFORE update
                    var oldEmployeeData = CaptureEmployeeData(existingEmployee);
                    
                    UpdateEmployeeFromRow(existingEmployee, row, companyId, 
                        designationId.Value, departmentId.Value, shiftBlockId.Value);
                    
                    existingEmployee.ModifiedDate = DateTime.UtcNow;
                    existingEmployee.ModifiedBy = User.Identity?.Name ?? "System";
                    
                    _context.Employees.Update(existingEmployee);
                    await _context.SaveChangesAsync();
                    
                    // ✅ NEW: Capture new data AFTER update
                    var newEmployeeData = CaptureEmployeeData(existingEmployee);
                    
                    // ✅ NEW: Create audit data with both old and new values
                    var auditData = new
                    {
                        OldValues = oldEmployeeData,
                        NewValues = newEmployeeData
                    };
                    
                    // ✅ MODIFIED: Log with both old and new data
                    await LogEmployeeOperation(existingEmployee.Id, "UPDATE", "Excel Import", auditData);
                    
                    var processedRow = new Dictionary<string, object>(row);
                    processedRow["_rowNumber"] = rowNumber;
                    processedRow["_status"] = "updated";
                    processedRow["_employeeId"] = existingEmployee.Id;
                    processedRow["EPFNo"] = existingEmployee.EPFNo;
                    result.ProcessedData.Add(processedRow);
                    
                    result.Successful++;
                    _logger.LogInformation($"✅ Row {rowNumber} updated successfully");
                }
                else
                {
                    _logger.LogInformation($"⏭️ Row {rowNumber} skipped (duplicate found)");
                    result.Skipped++;
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = rowNumber,
                        ErrorMessage = $"Duplicate employee found with {string.Join(", ", searchCriteria)} (skipped)",
                        RowData = row.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty)
                    });
                    
                    var skippedRow = new Dictionary<string, object>(row);
                    skippedRow["_rowNumber"] = rowNumber;
                    skippedRow["_status"] = "skipped";
                    skippedRow["_reason"] = "Duplicate found";
                    skippedRow["_duplicateEmployeeId"] = existingEmployee.Id;
                    skippedRow["_duplicateEPFNo"] = existingEmployee.EPFNo;
                    result.ProcessedData.Add(skippedRow);
                }
                
                continue;
            }

            _logger.LogInformation($"✅ No duplicate found. Creating new employee for row {rowNumber}");
            var employee = CreateEmployeeFromRow(row, companyId, 
                designationId.Value, departmentId.Value, shiftBlockId.Value);
            
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();
            
            var employeeId = employee.Id;
            
            var createdRow = new Dictionary<string, object>(row);
            createdRow["_rowNumber"] = rowNumber;
            createdRow["_status"] = "created";
            createdRow["_employeeId"] = employeeId;
            createdRow["EPFNo"] = employee.EPFNo;
            createdRow["AttendanceId"] = employee.AttendanceId;
            createdRow["EmployeeNo"] = employee.EmployeeNo;
            result.ProcessedData.Add(createdRow);
            
            // For CREATE, we just log the employee data (like before)
            await LogEmployeeOperation(employeeId, "CREATE", "Excel Import", employee);
            
            result.Successful++;
            _logger.LogInformation($"✅ Row {rowNumber} imported successfully");
            _logger.LogInformation($"Employee created: ID={employeeId}, EPFNo='{employee.EPFNo}', SystemName='{employee.SystemName}'");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ Error importing row {rowNumber}");
            
            var errorRow = new Dictionary<string, object>(row);
            errorRow["_rowNumber"] = rowNumber;
            errorRow["_status"] = "failed";
            errorRow["_error"] = ex.Message;
            result.ProcessedData.Add(errorRow);
            
            result.Errors.Add(new ImportError
            {
                RowNumber = rowNumber,
                ErrorMessage = ex.Message,
                RowData = row.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty)
            });
            result.Failed++;
        }
    }

    if (result.Successful > 0)
    {
        await _context.SaveChangesAsync();
        _logger.LogInformation($"✅ Saved {result.Successful} employees to database");
    }

    result.Success = result.Failed == 0;
    
    _logger.LogInformation($"=== IMPORT RESULTS ===");
    _logger.LogInformation($"Successful: {result.Successful}");
    _logger.LogInformation($"Failed: {result.Failed}");
    _logger.LogInformation($"Skipped: {result.Skipped}");
    _logger.LogInformation($"ProcessedData count: {result.ProcessedData.Count}");
    _logger.LogInformation($"=====================");
    
    return result;
}
// Add this method to capture employee data for audit
private Dictionary<string, object> CaptureEmployeeData(Employee employee)
{
    return new Dictionary<string, object>
    {
        ["Id"] = employee.Id,
        ["EmployeeNo"] = employee.EmployeeNo ?? string.Empty,
        ["EPFNo"] = employee.EPFNo ,
        ["AttendanceId"] = employee.AttendanceId ?? string.Empty,
        ["FirstName"] = employee.FirstName ?? string.Empty,
        ["LastName"] = employee.LastName ?? string.Empty,
        ["SystemName"] = employee.SystemName ?? string.Empty,
        ["NIC"] = employee.NIC ?? string.Empty,
        ["DesignationId"] = employee.DesignationId,
        ["DepartmentId"] = employee.DepartmentId,
        ["ShiftBlockId"] = employee.ShiftBlockId,
        ["BasicSalary"] = employee.BasicSalary,
        ["IsActive"] = employee.IsActive,
        ["RoleType"] = employee.RoleType ?? string.Empty,
        ["Mobile"] = employee.Mobile ?? string.Empty
    };
}
        private void LogRowData(Dictionary<string, object> row, string prefix = "Row")
        {
            _logger.LogInformation($"=== {prefix} DATA DEBUG ===");
            _logger.LogInformation($"Row keys: {string.Join(", ", row.Keys)}");
            
            foreach (var kvp in row)
            {
                var value = kvp.Value?.ToString() ?? "null";
                var type = kvp.Value?.GetType().Name ?? "null";
                _logger.LogInformation($"  {kvp.Key}: '{value}' (type: {type})");
            }
            _logger.LogInformation($"=== END {prefix} DATA DEBUG ===");
        }

        private async Task<int?> ResolveOrCreateDesignation(Dictionary<string, object> row, int companyId)
        {
            _logger.LogInformation($"=== RESOLVE DESIGNATION ===");
            _logger.LogInformation($"Row keys available: {string.Join(", ", row.Keys)}");
            
            var possibleKeys = new[] { "DesignationId", "Designation", "Name", "DesignationName", "JobTitle", "Position" };
            string designationName = null;
            string foundKey = null;
            
            foreach (var key in possibleKeys)
            {
                var value = GetStringValue(row, key);
                if (!string.IsNullOrEmpty(value))
                {
                    designationName = value;
                    foundKey = key;
                    break;
                }
            }
            
            _logger.LogInformation($"Looking for designation in keys: {string.Join(", ", possibleKeys)}");
            _logger.LogInformation($"Found designation in key '{foundKey}': '{designationName}'");
            
            if (string.IsNullOrEmpty(designationName))
            {
                _logger.LogInformation("No designation name found in any of the possible keys");
                return null;
            }

            designationName = designationName.Trim();
            _logger.LogInformation($"Resolving/Creating designation: '{designationName}' for company {companyId}");

            var existingDesignation = await _context.Company_Designations
                .FirstOrDefaultAsync(d => d.CompanyId == companyId && 
                    d.Designation.ToLower() == designationName.ToLower());

            if (existingDesignation != null)
            {
                _logger.LogInformation($"Found existing designation: {existingDesignation.Id} - '{existingDesignation.Designation}'");
                return existingDesignation.Id;
            }

            try
            {
                var newDesignation = new CompanyDesignation
                {
                    CompanyId = companyId,
                    Designation = designationName
                };

                _context.Company_Designations.Add(newDesignation);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Created new designation: {newDesignation.Id} - '{newDesignation.Designation}'");
                return newDesignation.Id;
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message?.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true ||
                    ex.InnerException?.Message?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var createdDesignation = await _context.Company_Designations
                        .FirstOrDefaultAsync(d => d.CompanyId == companyId && 
                            d.Designation.ToLower() == designationName.ToLower());
                    
                    if (createdDesignation != null)
                    {
                        _logger.LogInformation($"Found designation created by concurrent operation: {createdDesignation.Id}");
                        return createdDesignation.Id;
                    }
                }
                
                _logger.LogError(ex, $"Error creating designation '{designationName}'");
                return null;
            }
        }

        private async Task<int?> ResolveOrCreateDepartment(Dictionary<string, object> row, int companyId)
        {
            _logger.LogInformation($"=== RESOLVE DEPARTMENT ===");
            _logger.LogInformation($"Row keys available: {string.Join(", ", row.Keys)}");
            
            var possibleKeys = new[] { "DepartmentId", "Department", "DepartmentName", "Dept", "Division", "Unit" };
            string departmentName = null;
            string foundKey = null;
            
            foreach (var key in possibleKeys)
            {
                var value = GetStringValue(row, key);
                if (!string.IsNullOrEmpty(value))
                {
                    departmentName = value;
                    foundKey = key;
                    break;
                }
            }
            
            _logger.LogInformation($"Looking for department in keys: {string.Join(", ", possibleKeys)}");
            _logger.LogInformation($"Found department in key '{foundKey}': '{departmentName}'");
            
            if (string.IsNullOrEmpty(departmentName))
            {
                _logger.LogInformation("No department name found in any of the possible keys");
                return null;
            }

            departmentName = departmentName.Trim();
            _logger.LogInformation($"Resolving/Creating department: '{departmentName}' for company {companyId}");

            var existingDepartment = await _context.Company_Departments
                .FirstOrDefaultAsync(d => d.CompanyId == companyId && 
                    d.DepartmentName.ToLower() == departmentName.ToLower());

            if (existingDepartment != null)
            {
                _logger.LogInformation($"Found existing department: {existingDepartment.Id} - '{existingDepartment.DepartmentName}'");
                return existingDepartment.Id;
            }

            try
            {
                var newDepartment = new CompanyDepartments
                {
                    CompanyId = companyId,
                    DepartmentName = departmentName
                };

                _context.Company_Departments.Add(newDepartment);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Created new department: {newDepartment.Id} - '{newDepartment.DepartmentName}'");
                return newDepartment.Id;
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message?.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true ||
                    ex.InnerException?.Message?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var createdDepartment = await _context.Company_Departments
                        .FirstOrDefaultAsync(d => d.CompanyId == companyId && 
                            d.DepartmentName.ToLower() == departmentName.ToLower());
                    
                    if (createdDepartment != null)
                    {
                        _logger.LogInformation($"Found department created by concurrent operation: {createdDepartment.Id}");
                        return createdDepartment.Id;
                    }
                }
                
                _logger.LogError(ex, $"Error creating department '{departmentName}'");
                return null;
            }
        }

        private async Task<int?> ResolveOrCreateShiftBlock(Dictionary<string, object> row, int companyId)
        {
            _logger.LogInformation($"=== RESOLVE SHIFT BLOCK ===");
            _logger.LogInformation($"Row keys available: {string.Join(", ", row.Keys)}");
            
            var possibleKeys = new[] { "ShiftBlockId", "ShiftBlock", "Shift", "ShiftName", "Schedule" };
            string shiftBlockName = null;
            string foundKey = null;
            
            foreach (var key in possibleKeys)
            {
                var value = GetStringValue(row, key);
                if (!string.IsNullOrEmpty(value))
                {
                    shiftBlockName = value;
                    foundKey = key;
                    break;
                }
            }
            
            _logger.LogInformation($"Looking for shift block in keys: {string.Join(", ", possibleKeys)}");
            _logger.LogInformation($"Found shift block in key '{foundKey}': '{shiftBlockName}'");
            
            if (string.IsNullOrEmpty(shiftBlockName))
            {
                _logger.LogInformation("No shift block name found in any of the possible keys");
                return null;
            }

            shiftBlockName = shiftBlockName.Trim();
            _logger.LogInformation($"Resolving/Creating shift block: '{shiftBlockName}' for company {companyId}");

            var existingShiftBlock = await _context.ShiftBlocks
                .FirstOrDefaultAsync(s => s.CompanyId == companyId && 
                    s.ShiftName.ToLower() == shiftBlockName.ToLower() && 
                    s.IsActive);

            if (existingShiftBlock != null)
            {
                _logger.LogInformation($"Found existing shift block: {existingShiftBlock.Id} - '{existingShiftBlock.ShiftName}'");
                return existingShiftBlock.Id;
            }

            try
            {
                var newShiftBlock = new ShiftBlock
                {
                    CompanyId = companyId,
                    ShiftName = shiftBlockName,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    IsAttendance = false
                };

                _context.ShiftBlocks.Add(newShiftBlock);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Created new shift block: {newShiftBlock.Id} - '{newShiftBlock.ShiftName}'");
                return newShiftBlock.Id;
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message?.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true ||
                    ex.InnerException?.Message?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var createdShiftBlock = await _context.ShiftBlocks
                        .FirstOrDefaultAsync(s => s.CompanyId == companyId && 
                            s.ShiftName.ToLower() == shiftBlockName.ToLower() && 
                            s.IsActive);
                    
                    if (createdShiftBlock != null)
                    {
                        _logger.LogInformation($"Found shift block created by concurrent operation: {createdShiftBlock.Id}");
                        return createdShiftBlock.Id;
                    }
                }
                
                _logger.LogError(ex, $"Error creating shift block '{shiftBlockName}'");
                return null;
            }
        }

        private async Task<int?> ResolveOrCreateDesignationFromRow(
            Dictionary<string, object> row, 
            int companyId, 
            Dictionary<string, int> designations)
        {
            var possibleKeys = new[] { "DesignationId", "Designation", "Name", "DesignationName", "JobTitle", "Position" };
            string designationName = null;
            
            foreach (var key in possibleKeys)
            {
                var value = GetStringValue(row, key);
                if (!string.IsNullOrEmpty(value))
                {
                    designationName = value;
                    break;
                }
            }
            
            if (string.IsNullOrEmpty(designationName))
                return null;

            designationName = designationName.Trim();

            if (designations.TryGetValue(designationName.ToLower(), out int id))
                return id;

            try
            {
                var newDesignation = new CompanyDesignation
                {
                    CompanyId = companyId,
                    Designation = designationName
                };

                _context.Company_Designations.Add(newDesignation);
                await _context.SaveChangesAsync();
                
                designations[designationName.ToLower()] = newDesignation.Id;
                
                _logger.LogInformation($"Created new designation: {newDesignation.Id} - '{newDesignation.Designation}'");
                return newDesignation.Id;
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message?.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true ||
                    ex.InnerException?.Message?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var createdDesignation = await _context.Company_Designations
                        .FirstOrDefaultAsync(d => d.CompanyId == companyId && 
                            d.Designation.ToLower() == designationName.ToLower());
                    
                    if (createdDesignation != null)
                    {
                        designations[designationName.ToLower()] = createdDesignation.Id;
                        return createdDesignation.Id;
                    }
                }
                
                _logger.LogError(ex, $"Error creating designation '{designationName}'");
                return null;
            }
        }

        private async Task<int?> ResolveOrCreateDepartmentFromRow(
            Dictionary<string, object> row, 
            int companyId, 
            Dictionary<string, int> departments)
        {
            var possibleKeys = new[] { "DepartmentId", "Department", "DepartmentName", "Dept", "Division", "Unit" };
            string departmentName = null;
            
            foreach (var key in possibleKeys)
            {
                var value = GetStringValue(row, key);
                if (!string.IsNullOrEmpty(value))
                {
                    departmentName = value;
                    break;
                }
            }
            
            if (string.IsNullOrEmpty(departmentName))
                return null;

            departmentName = departmentName.Trim();

            if (departments.TryGetValue(departmentName.ToLower(), out int id))
                return id;

            try
            {
                var newDepartment = new CompanyDepartments
                {
                    CompanyId = companyId,
                    DepartmentName = departmentName
                };

                _context.Company_Departments.Add(newDepartment);
                await _context.SaveChangesAsync();
                
                departments[departmentName.ToLower()] = newDepartment.Id;
                
                _logger.LogInformation($"Created new department: {newDepartment.Id} - '{newDepartment.DepartmentName}'");
                return newDepartment.Id;
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message?.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true ||
                    ex.InnerException?.Message?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var createdDepartment = await _context.Company_Departments
                        .FirstOrDefaultAsync(d => d.CompanyId == companyId && 
                            d.DepartmentName.ToLower() == departmentName.ToLower());
                    
                    if (createdDepartment != null)
                    {
                        departments[departmentName.ToLower()] = createdDepartment.Id;
                        return createdDepartment.Id;
                    }
                }
                
                _logger.LogError(ex, $"Error creating department '{departmentName}'");
                return null;
            }
        }

        private async Task<int?> ResolveOrCreateShiftBlockFromRow(
            Dictionary<string, object> row, 
            int companyId, 
            Dictionary<string, int> shiftBlocks)
        {
            var possibleKeys = new[] { "ShiftBlockId", "ShiftBlock", "Shift", "ShiftName", "Schedule" };
            string shiftBlockName = null;
            
            foreach (var key in possibleKeys)
            {
                var value = GetStringValue(row, key);
                if (!string.IsNullOrEmpty(value))
                {
                    shiftBlockName = value;
                    break;
                }
            }
            
            if (string.IsNullOrEmpty(shiftBlockName))
                return null;

            shiftBlockName = shiftBlockName.Trim();

            if (shiftBlocks.TryGetValue(shiftBlockName.ToLower(), out int id))
                return id;

            try
            {
                var newShiftBlock = new ShiftBlock
                {
                    CompanyId = companyId,
                    ShiftName = shiftBlockName,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    IsAttendance = false
                };

                _context.ShiftBlocks.Add(newShiftBlock);
                await _context.SaveChangesAsync();
                
                shiftBlocks[shiftBlockName.ToLower()] = newShiftBlock.Id;
                
                _logger.LogInformation($"Created new shift block: {newShiftBlock.Id} - '{newShiftBlock.ShiftName}'");
                return newShiftBlock.Id;
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message?.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true ||
                    ex.InnerException?.Message?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var createdShiftBlock = await _context.ShiftBlocks
                        .FirstOrDefaultAsync(s => s.CompanyId == companyId && 
                            s.ShiftName.ToLower() == shiftBlockName.ToLower() && 
                            s.IsActive);
                    
                    if (createdShiftBlock != null)
                    {
                        shiftBlocks[shiftBlockName.ToLower()] = createdShiftBlock.Id;
                        return createdShiftBlock.Id;
                    }
                }
                
                _logger.LogError(ex, $"Error creating shift block '{shiftBlockName}'");
                return null;
            }
        }

        private async Task<int> GetDefaultDesignationId(int companyId)
        {
            var defaultDesignation = await _context.Company_Designations
                .FirstOrDefaultAsync(d => d.CompanyId == companyId);

            if (defaultDesignation != null)
                return defaultDesignation.Id;

            var newDesignation = new CompanyDesignation
            {
                CompanyId = companyId,
                Designation = "Default Designation"
            };

            _context.Company_Designations.Add(newDesignation);
            await _context.SaveChangesAsync();
            return newDesignation.Id;
        }

        private async Task<int> GetDefaultDepartmentId(int companyId)
        {
            var defaultDepartment = await _context.Company_Departments
                .FirstOrDefaultAsync(d => d.CompanyId == companyId);

            if (defaultDepartment != null)
                return defaultDepartment.Id;

            var newDepartment = new CompanyDepartments
            {
                CompanyId = companyId,
                DepartmentName = "Default Department"
            };

            _context.Company_Departments.Add(newDepartment);
            await _context.SaveChangesAsync();
            return newDepartment.Id;
        }

        private async Task<int> GetDefaultShiftBlockId(int companyId)
        {
            var defaultShiftBlock = await _context.ShiftBlocks
                .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.IsActive);

            if (defaultShiftBlock != null)
                return defaultShiftBlock.Id;

            var newShiftBlock = new ShiftBlock
            {
                CompanyId = companyId,
                ShiftName = "Default Shift",
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                IsAttendance = false
            };

            _context.ShiftBlocks.Add(newShiftBlock);
            await _context.SaveChangesAsync();
            return newShiftBlock.Id;
        }

        private Employee CreateEmployeeFromRow(
            Dictionary<string, object> row,
            int companyId,
            int designationId,
            int departmentId,
            int shiftBlockId)
        {
            _logger.LogInformation("=== CREATING EMPLOYEE FROM ROW ===");
            
            var employee = new Employee
            {
                CompanyId = companyId,
                DesignationId = designationId,
                DepartmentId = departmentId,
                ShiftBlockId = shiftBlockId,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "System",
                ModifiedBy = User.Identity?.Name ?? "System"
            };
var companySettings = _companySettingsService.GetCompanySettingsAsync(companyId).GetAwaiter().GetResult();
            var epfNo = GetStringValue(row, "EPFNo");
            if (!string.IsNullOrEmpty(epfNo))
            {
                epfNo = System.Text.RegularExpressions.Regex.Replace(epfNo, @"[^0-9]", "");
                epfNo = epfNo.TrimStart('0');
                if (string.IsNullOrEmpty(epfNo)) epfNo = GetStringValue(row, "EPFNo");
            }
            else
            {
                epfNo = GenerateEPFNumber();
            }
            employee.EPFNo = int.Parse(epfNo);

            var firstName = GetStringValue(row, "FirstName");
            employee.FirstName = firstName ?? "";

            var lastName = GetStringValue(row, "LastName");
            employee.LastName = lastName ?? "";

            var systemName = GetStringValue(row, "SystemName");
            if (string.IsNullOrEmpty(systemName))
            {
                if (!string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName))
                {
                    systemName = (firstName + " " + lastName).Trim();
                }
                else
                {
                    systemName = "EMP" + epfNo;
                }
                _logger.LogInformation($"SystemName not provided, generated: {systemName}");
            }
            employee.SystemName = systemName;

            var attendanceId = GetStringValue(row, "AttendanceId");
            if (string.IsNullOrEmpty(attendanceId))
            {
                attendanceId = epfNo;
            }
            employee.AttendanceId = attendanceId;

            var employeeNo = GetStringValue(row, "EmployeeNo");
            if (string.IsNullOrEmpty(employeeNo))
            {
                employeeNo = attendanceId;
            }
            employee.EmployeeNo = employeeNo;

            employee.Title = GetStringValue(row, "Title");
            employee.Initial = GetStringValue(row, "Initial");
            employee.NIC = GetStringValue(row, "NIC");
            employee.DOB = GetDateTimeValue(row, "DOB");
            employee.Gender = GetStringValue(row, "Gender");
            employee.MaritalStatus = GetStringValue(row, "MaritalStatus");
            employee.BloodGroup = GetStringValue(row, "BloodGroup");
            employee.DrivingLicense = GetStringValue(row, "DrivingLicense");
            employee.Religion = GetStringValue(row, "Religion");
            employee.Nationality = GetStringValue(row, "Nationality");
            employee.Race = GetStringValue(row, "Race");
            employee.Mobile = GetStringValue(row, "Mobile");
            employee.LandPhone = GetStringValue(row, "LandPhone");
            employee.ContactNo = GetStringValue(row, "ContactNo");
            employee.ResidentialAddress = GetStringValue(row, "ResidentialAddress");
            employee.PermanentAddress = GetStringValue(row, "PermanentAddress");
            var budgetaryAllowanceValue = GetDecimalValue(row, "BudgetaryAllowance");
    employee.BudgetaryAllowance = budgetaryAllowanceValue > 0 ? budgetaryAllowanceValue : companySettings.MinBudgetaryAllowanceOne;
    
    // Handle BudgetaryAllowance2 - use company default if not provided
    var budgetaryAllowance2Value = GetDecimalValue(row, "BudgetaryAllowance2");
    employee.BudgetaryAllowance2 = budgetaryAllowance2Value > 0 ? budgetaryAllowance2Value : companySettings.MinBudgetaryAllowanceTwo;

    // Also handle BasicSalary if not provided
    var basicSalaryValue = GetDecimalValue(row, "BasicSalary");
    employee.BasicSalary = basicSalaryValue > 0 ? basicSalaryValue : companySettings.MinBasic;

            employee.AttendanceAllowance = GetDecimalValue(row, "AttendanceAllowance");
            employee.FixedAllowance = GetDecimalValue(row, "FixedAllowance");
            employee.MealAllowance = GetDecimalValue(row, "MealAllowance");
            employee.SpecialAllowance = GetDecimalValue(row, "SpecialAllowance");
            employee.TransportAllowance = GetDecimalValue(row, "TransportAllowance");
            employee.AccommodationAllowance = GetDecimalValue(row, "AccommodationAllowance");
            employee.FuelAllowance = GetDecimalValue(row, "FuelAllowance");
            employee.CostOfLivingAllowance = GetDecimalValue(row, "CostOfLivingAllowance");
            employee.PerformanceAllowance = GetDecimalValue(row, "PerformanceAllowance");
            employee.HealthAllowance = GetDecimalValue(row, "HealthAllowance");
            
            employee.SalaryPayType = GetStringValue(row, "SalaryPayType") ?? "CashPay";
            employee.AccountNumber = GetStringValue(row, "AccountNumber");
            employee.BankAccountName = GetStringValue(row, "BankAccountName");
            employee.BankCode = GetStringValue(row, "BankCode");
            employee.BankName = GetStringValue(row, "BankName");
            employee.BranchCode = GetStringValue(row, "BranchCode");
            employee.BranchName = GetStringValue(row, "BranchName");
            
            employee.DateOfAppointment = GetDateTimeValue(row, "DateOfAppointment");
            
            employee.EPFPay = GetBoolValue(row, "EPFPay");
            employee.Probation = GetBoolValue(row, "Probation");
            employee.Block = GetBoolValue(row, "Block");
            employee.Resigned = GetBoolValue(row, "Resigned");
            employee.BlockAttendance = GetBoolValue(row, "BlockAttendance");
            
            employee.ProbationPeriod = GetIntValue(row, "ProbationPeriod");
            employee.ProbationEndDate = GetDateTimeValue(row, "ProbationEndDate");
            employee.ReviewedBy = GetStringValue(row, "ReviewedBy");
            
            employee.BlockUntil = GetStringValue(row, "BlockUntil");
            employee.BlockReason = GetStringValue(row, "BlockReason");
            employee.BlockRemark = GetStringValue(row, "BlockRemark");
            
            employee.ResignedDate = GetDateTimeValue(row, "ResignedDate");
            employee.ExitType = GetStringValue(row, "ExitType");
            employee.ExitReason = GetStringValue(row, "ExitReason");
            employee.ExitRemark = GetStringValue(row, "ExitRemark");
            
            employee.Keen1ContactName = GetStringValue(row, "Keen1ContactName");
            employee.Keen1ContactNumber = GetStringValue(row, "Keen1ContactNumber");
            employee.Keen1Relationship = GetStringValue(row, "Keen1Relationship");
            employee.Keen1Address = GetStringValue(row, "Keen1Address");
            employee.Keen1Position = GetStringValue(row, "Keen1Position");
            employee.Keen1WorkPlace = GetStringValue(row, "Keen1WorkPlace");
            employee.Keen1WorkPlaceContact = GetStringValue(row, "Keen1WorkPlaceContact");
            
            employee.Keen2ContactName = GetStringValue(row, "Keen2ContactName");
            employee.Keen2ContactNumber = GetStringValue(row, "Keen2ContactNumber");
            employee.Keen2Relationship = GetStringValue(row, "Keen2Relationship");
            employee.Keen2Address = GetStringValue(row, "Keen2Address");
            employee.Keen2Position = GetStringValue(row, "Keen2Position");
            employee.Keen2WorkPlace = GetStringValue(row, "Keen2WorkPlace");
            employee.Keen2WorkPlaceContact = GetStringValue(row, "Keen2WorkPlaceContact");

            employee.RoleType = GetStringValue(row, "RoleType") ?? "Employee";
            employee.LeaveApproval = GetStringValue(row, "LeaveApproval");
            employee.FinanceApproval = GetStringValue(row, "FinanceApproval");
            
            employee.OccupationNo = GetIntValue(row, "OccupationNo");
            employee.OccupationGrade = GetIntValue(row, "OccupationGrade");

            _logger.LogInformation($"Employee created: EPFNo='{employee.EPFNo}', AttendanceId='{employee.AttendanceId}', EmployeeNo='{employee.EmployeeNo}'");
            _logger.LogInformation($"FirstName='{employee.FirstName}', LastName='{employee.LastName}', SystemName='{employee.SystemName}'");
            _logger.LogInformation($"DesignationId={designationId}, DepartmentId={departmentId}, ShiftBlockId={shiftBlockId}");

            return employee;
        }

        

        private async Task<int> GetDefaultCompanyId()
        {
            try
            {
                var company = await _context.Companies
                    .Select(c => new { c.Id })
                    .FirstOrDefaultAsync();
                
                if (company != null)
                    return company.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default company");
            }

            try
            {
                var newCompany = new Company
                {
                    CompanyName = "Default Company",
                    CompanyRegisterd = DateTime.UtcNow,
                    CompanyAddress = "Default Address",
                    TelephonNuber = "0000000000"
                };
                _context.Companies.Add(newCompany);
                await _context.SaveChangesAsync();
                return newCompany.Id;
            }
            catch
            {
                return 1;
            }
        }

      private async Task LogEmployeeOperation(int employeeId, string operation, string source, object data)
{
    try
    {
        if (employeeId <= 0)
        {
            _logger.LogWarning($"Cannot log operation for employee with invalid ID: {employeeId}");
            return;
        }

        var log = new EmployeeAuditLog
        {
            EmployeeId = employeeId,
            Operation = operation,
            Source = source,
            PerformedBy = User.Identity?.Name ?? "System",
            PerformedDate = DateTime.UtcNow,
            RowData = JsonSerializer.Serialize(data)
        };
        
        _context.EmployeeAuditLogs.Add(log);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Logged {operation} for employee {employeeId}");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error logging employee operation for employee {employeeId}");
    }
}
private Employee UpdateEmployeeFromRow(
    Employee employee,
    Dictionary<string, object> row,
    int companyId,
    int designationId,
    int departmentId,
    int shiftBlockId)
{
    _logger.LogInformation("=== UPDATING EMPLOYEE FROM ROW ===");
    
    var epfNo = GetStringValue(row, "EPFNo");
    if (!string.IsNullOrEmpty(epfNo))
    {
        epfNo = System.Text.RegularExpressions.Regex.Replace(epfNo, @"[^0-9]", "");
        epfNo = epfNo.TrimStart('0');
        if (string.IsNullOrEmpty(epfNo)) epfNo = GetStringValue(row, "EPFNo");
        employee.EPFNo = int.Parse(epfNo);
    }

    var firstName = GetStringValue(row, "FirstName");
    if (!string.IsNullOrEmpty(firstName))
    {
        employee.FirstName = firstName;
    }
    
    var lastName = GetStringValue(row, "LastName");
    if (!string.IsNullOrEmpty(lastName))
    {
        employee.LastName = lastName;
    }

    var systemName = GetStringValue(row, "SystemName");
    if (!string.IsNullOrEmpty(systemName))
    {
        employee.SystemName = systemName;
    }
    else
    {
        var fn = employee.FirstName ?? "";
        var ln = employee.LastName ?? "";
        if (!string.IsNullOrEmpty(fn) || !string.IsNullOrEmpty(ln))
        {
            employee.SystemName = (fn + " " + ln).Trim();
        }
    }

    employee.DesignationId = designationId;
    employee.DepartmentId = departmentId;
    employee.ShiftBlockId = shiftBlockId;

    var attendanceId = GetStringValue(row, "AttendanceId");
    if (!string.IsNullOrEmpty(attendanceId))
    {
        employee.AttendanceId = attendanceId;
    }

    var employeeNo = GetStringValue(row, "EmployeeNo");
    if (!string.IsNullOrEmpty(employeeNo))
    {
        employee.EmployeeNo = employeeNo;
    }

    employee.Title = GetStringValue(row, "Title") ?? employee.Title;
    employee.Initial = GetStringValue(row, "Initial") ?? employee.Initial;
    employee.NIC = GetStringValue(row, "NIC") ?? employee.NIC;
    
    var dob = GetDateTimeValue(row, "DOB");
    if (dob.HasValue) employee.DOB = dob;
    
    employee.Gender = GetStringValue(row, "Gender") ?? employee.Gender;
    employee.MaritalStatus = GetStringValue(row, "MaritalStatus") ?? employee.MaritalStatus;
    employee.BloodGroup = GetStringValue(row, "BloodGroup") ?? employee.BloodGroup;
    employee.DrivingLicense = GetStringValue(row, "DrivingLicense") ?? employee.DrivingLicense;
    employee.Religion = GetStringValue(row, "Religion") ?? employee.Religion;
    employee.Nationality = GetStringValue(row, "Nationality") ?? employee.Nationality;
    employee.Race = GetStringValue(row, "Race") ?? employee.Race;
    employee.Mobile = GetStringValue(row, "Mobile") ?? employee.Mobile;
    employee.LandPhone = GetStringValue(row, "LandPhone") ?? employee.LandPhone;
    employee.ContactNo = GetStringValue(row, "ContactNo") ?? employee.ContactNo;
    employee.ResidentialAddress = GetStringValue(row, "ResidentialAddress") ?? employee.ResidentialAddress;
    employee.PermanentAddress = GetStringValue(row, "PermanentAddress") ?? employee.PermanentAddress;
    
    // Update salary fields if present
    if (row.ContainsKey("BasicSalary")) 
    {
        var basicSalaryValue = GetDecimalValue(row, "BasicSalary");
        if (basicSalaryValue > 0) employee.BasicSalary = basicSalaryValue;
    }
    if (row.ContainsKey("AttendanceAllowance")) 
        employee.AttendanceAllowance = GetDecimalValue(row, "AttendanceAllowance");
    if (row.ContainsKey("FixedAllowance")) 
        employee.FixedAllowance = GetDecimalValue(row, "FixedAllowance");
    if (row.ContainsKey("MealAllowance")) 
        employee.MealAllowance = GetDecimalValue(row, "MealAllowance");
    if (row.ContainsKey("SpecialAllowance")) 
        employee.SpecialAllowance = GetDecimalValue(row, "SpecialAllowance");
    if (row.ContainsKey("TransportAllowance")) 
        employee.TransportAllowance = GetDecimalValue(row, "TransportAllowance");
    if (row.ContainsKey("AccommodationAllowance")) 
        employee.AccommodationAllowance = GetDecimalValue(row, "AccommodationAllowance");
    if (row.ContainsKey("FuelAllowance")) 
        employee.FuelAllowance = GetDecimalValue(row, "FuelAllowance");
    if (row.ContainsKey("CostOfLivingAllowance")) 
        employee.CostOfLivingAllowance = GetDecimalValue(row, "CostOfLivingAllowance");
    if (row.ContainsKey("PerformanceAllowance")) 
        employee.PerformanceAllowance = GetDecimalValue(row, "PerformanceAllowance");
    if (row.ContainsKey("HealthAllowance")) 
        employee.HealthAllowance = GetDecimalValue(row, "HealthAllowance");
    
    if (row.ContainsKey("BudgetaryAllowance"))
    {
        var budgetaryValue = GetDecimalValue(row, "BudgetaryAllowance");
        if (budgetaryValue > 0) employee.BudgetaryAllowance = budgetaryValue;
    }
    if (row.ContainsKey("BudgetaryAllowance2"))
    {
        var budgetary2Value = GetDecimalValue(row, "BudgetaryAllowance2");
        if (budgetary2Value > 0) employee.BudgetaryAllowance2 = budgetary2Value;
    }
    
    employee.SalaryPayType = GetStringValue(row, "SalaryPayType") ?? employee.SalaryPayType;
    employee.AccountNumber = GetStringValue(row, "AccountNumber") ?? employee.AccountNumber;
    employee.BankAccountName = GetStringValue(row, "BankAccountName") ?? employee.BankAccountName;
    employee.BankCode = GetStringValue(row, "BankCode") ?? employee.BankCode;
    employee.BankName = GetStringValue(row, "BankName") ?? employee.BankName;
    employee.BranchCode = GetStringValue(row, "BranchCode") ?? employee.BranchCode;
    employee.BranchName = GetStringValue(row, "BranchName") ?? employee.BranchName;
    
    var dateOfAppointment = GetDateTimeValue(row, "DateOfAppointment");
    if (dateOfAppointment.HasValue) employee.DateOfAppointment = dateOfAppointment;
    
    if (row.ContainsKey("EPFPay")) employee.EPFPay = GetBoolValue(row, "EPFPay");
    if (row.ContainsKey("Probation")) employee.Probation = GetBoolValue(row, "Probation");
    if (row.ContainsKey("Block")) employee.Block = GetBoolValue(row, "Block");
    if (row.ContainsKey("Resigned")) employee.Resigned = GetBoolValue(row, "Resigned");
    if (row.ContainsKey("BlockAttendance")) employee.BlockAttendance = GetBoolValue(row, "BlockAttendance");
    
    var probationPeriod = GetIntValue(row, "ProbationPeriod");
    if (probationPeriod.HasValue) employee.ProbationPeriod = probationPeriod;
    
    var probationEndDate = GetDateTimeValue(row, "ProbationEndDate");
    if (probationEndDate.HasValue) employee.ProbationEndDate = probationEndDate;
    
    employee.ReviewedBy = GetStringValue(row, "ReviewedBy") ?? employee.ReviewedBy;
    
    employee.BlockUntil = GetStringValue(row, "BlockUntil") ?? employee.BlockUntil;
    employee.BlockReason = GetStringValue(row, "BlockReason") ?? employee.BlockReason;
    employee.BlockRemark = GetStringValue(row, "BlockRemark") ?? employee.BlockRemark;
    
    var resignedDate = GetDateTimeValue(row, "ResignedDate");
    if (resignedDate.HasValue) employee.ResignedDate = resignedDate;
    
    employee.ExitType = GetStringValue(row, "ExitType") ?? employee.ExitType;
    employee.ExitReason = GetStringValue(row, "ExitReason") ?? employee.ExitReason;
    employee.ExitRemark = GetStringValue(row, "ExitRemark") ?? employee.ExitRemark;
    
    employee.Keen1ContactName = GetStringValue(row, "Keen1ContactName") ?? employee.Keen1ContactName;
    employee.Keen1ContactNumber = GetStringValue(row, "Keen1ContactNumber") ?? employee.Keen1ContactNumber;
    employee.Keen1Relationship = GetStringValue(row, "Keen1Relationship") ?? employee.Keen1Relationship;
    employee.Keen1Address = GetStringValue(row, "Keen1Address") ?? employee.Keen1Address;
    employee.Keen1Position = GetStringValue(row, "Keen1Position") ?? employee.Keen1Position;
    employee.Keen1WorkPlace = GetStringValue(row, "Keen1WorkPlace") ?? employee.Keen1WorkPlace;
    employee.Keen1WorkPlaceContact = GetStringValue(row, "Keen1WorkPlaceContact") ?? employee.Keen1WorkPlaceContact;
    
    employee.Keen2ContactName = GetStringValue(row, "Keen2ContactName") ?? employee.Keen2ContactName;
    employee.Keen2ContactNumber = GetStringValue(row, "Keen2ContactNumber") ?? employee.Keen2ContactNumber;
    employee.Keen2Relationship = GetStringValue(row, "Keen2Relationship") ?? employee.Keen2Relationship;
    employee.Keen2Address = GetStringValue(row, "Keen2Address") ?? employee.Keen2Address;
    employee.Keen2Position = GetStringValue(row, "Keen2Position") ?? employee.Keen2Position;
    employee.Keen2WorkPlace = GetStringValue(row, "Keen2WorkPlace") ?? employee.Keen2WorkPlace;
    employee.Keen2WorkPlaceContact = GetStringValue(row, "Keen2WorkPlaceContact") ?? employee.Keen2WorkPlaceContact;

    employee.RoleType = GetStringValue(row, "RoleType") ?? employee.RoleType;
    employee.LeaveApproval = GetStringValue(row, "LeaveApproval") ?? employee.LeaveApproval;
    employee.FinanceApproval = GetStringValue(row, "FinanceApproval") ?? employee.FinanceApproval;
    
    var occupationNo = GetIntValue(row, "OccupationNo");
    if (occupationNo.HasValue) employee.OccupationNo = occupationNo;
    
    var occupationGrade = GetIntValue(row, "OccupationGrade");
    if (occupationGrade.HasValue) employee.OccupationGrade = occupationGrade;

    _logger.LogInformation($"Updated employee: EPFNo='{employee.EPFNo}', SystemName='{employee.SystemName}'");
    
    return employee;
}
        #endregion

        #region Helper Methods

        private string? GetStringValue(Dictionary<string, object> data, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (data.TryGetValue(key, out var value) && value != null)
            {
                var str = value.ToString()?.Trim();
                return string.IsNullOrEmpty(str) ? null : str;
            }
            return null;
        }

        private DateTime? GetDateTimeValue(Dictionary<string, object> data, string key)
        {
            var str = GetStringValue(data, key);
            if (!string.IsNullOrEmpty(str))
            {
                if (DateTime.TryParse(str, out var date))
                    return date;
                if (DateTime.TryParseExact(str, new[] { "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "M/d/yyyy" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dateExact))
                    return dateExact;
            }
            return null;
        }

        private decimal GetDecimalValue(Dictionary<string, object> data, string key)
        {
            var str = GetStringValue(data, key);
            if (!string.IsNullOrEmpty(str) && decimal.TryParse(str, out var value))
                return value;
            return 0;
        }
private double GetDoubleValue(Dictionary<string, object> data, string key)
{
    var str = GetStringValue(data, key);
    if (!string.IsNullOrEmpty(str) && double.TryParse(str, out var value))
        return value;
    return 0;
}
        private int? GetIntValue(Dictionary<string, object> data, string key)
        {
            var str = GetStringValue(data, key);
            if (!string.IsNullOrEmpty(str) && int.TryParse(str, out var value))
                return value;
            return null;
        }

        private bool GetBoolValue(Dictionary<string, object> data, string key)
        {
            var str = GetStringValue(data, key);
            if (!string.IsNullOrEmpty(str))
            {
                var lower = str.ToLower().Trim();
                return lower == "true" || lower == "1" || lower == "yes" || lower == "y" || lower == "on";
            }
            return false;
        }

        private string GenerateEPFNumber()
        {
            var random = new Random();
            return random.Next(10000000, 99999999).ToString();
        }

        #endregion

        #region Request/Response Models

        public class ConfirmMappingRequest
        {
            public string UploadId { get; set; } = string.Empty;
            public Dictionary<string, string> Mappings { get; set; } = new Dictionary<string, string>();
            public bool OverrideDuplicates { get; set; }
            public int? CompanyId { get; set; }
            public List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();
        }

        public class ApiResponse<T>
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public T? Data { get; set; }
        }

        public class ExcelPreviewData
        {
            public List<string> Columns { get; set; } = new List<string>();
            public List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();
            public List<ExcelColumnMapping> ColumnMappings { get; set; } = new List<ExcelColumnMapping>();
            public int TotalRows { get; set; }
            public string FileName { get; set; } = string.Empty;
            public string? UploadId { get; set; }
            public bool IsRetry { get; set; }
            public string? OriginalUploadId { get; set; }
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


        public class ImportJobResponse
        {
            public string JobId { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

        public class ImportJobStatus
        {
            public string JobId { get; set; } = string.Empty;
            public string Status { get; set; } = "Pending";
            public int TotalRows { get; set; }
            public int Processed { get; set; }
            public int Successful { get; set; }
            public int Failed { get; set; }
            public int Skipped { get; set; }
            public double Progress { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public bool IsComplete { get; set; }
            public string? ErrorMessage { get; set; }
            public List<ImportError> Errors { get; set; } = new();
        }

        #endregion
    }
}