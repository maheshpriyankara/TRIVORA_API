using Microsoft.AspNetCore.Mvc;
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

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExcelImportController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ExcelImportController> _logger;
        private static readonly Dictionary<string, ExcelPreviewData> _importCache = new();

        public ExcelImportController(
            ApplicationDbContext context,
            ILogger<ExcelImportController> logger)
        {
            _context = context;
            _logger = logger;
        }

       [HttpPost("upload")]
public async Task<ActionResult<ApiResponse<ExcelPreviewData>>> UploadExcel([FromForm] IFormFile file)
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

        // ========== READ EDITED DATA FROM FORM ==========
        // ========== READ EDITED DATA FROM FORM ==========
ExcelPreviewData editedData = null;
bool hasEditedData = false;

// Try to get editedData from form
if (Request.Form.ContainsKey("editedData"))
{
    var editedDataJson = Request.Form["editedData"].FirstOrDefault();
    _logger.LogInformation($"Received editedData JSON length: {editedDataJson?.Length ?? 0}");
    
    if (!string.IsNullOrEmpty(editedDataJson))
    {
        try
        {
            // Log the first 200 characters for debugging
            _logger.LogInformation($"EditedData JSON (first 200 chars): {editedDataJson.Substring(0, Math.Min(200, editedDataJson.Length))}");
            
            // ========== FIX: Parse as dictionary first ==========
            var jsonObject = JsonSerializer.Deserialize<Dictionary<string, object>>(editedDataJson);
            
            if (jsonObject != null && jsonObject.ContainsKey("rows"))
            {
                // Extract rows
                var rowsJson = jsonObject["rows"].ToString();
                var rows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(rowsJson);
                
                if (rows != null && rows.Count > 0)
                {
                    editedData = new ExcelPreviewData();
                    editedData.Rows = rows;
                    
                    // Extract columns if present
                    if (jsonObject.ContainsKey("columns"))
                    {
                        var columnsJson = jsonObject["columns"].ToString();
                        editedData.Columns = JsonSerializer.Deserialize<List<string>>(columnsJson) ?? new List<string>();
                    }
                    else
                    {
                        // Generate columns from first row
                        if (rows.Count > 0)
                        {
                            editedData.Columns = rows[0].Keys.ToList();
                        }
                    }
                    
                    hasEditedData = true;
                    _logger.LogInformation($"✅ Successfully parsed edited data with {rows.Count} rows and {editedData.Columns.Count} columns");
                }
                else
                {
                    _logger.LogWarning("❌ Parsed rows but they were null or empty");
                }
            }
            else
            {
                _logger.LogWarning("❌ JSON missing 'rows' property");
                
                // Try to deserialize as ExcelPreviewData directly
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
else
{
    _logger.LogWarning("⚠️ No 'editedData' key found in form");
    _logger.LogInformation("All form keys: " + string.Join(", ", Request.Form.Keys));
}

        // If no edited data, read from Excel file
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

            // Read headers from first row
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

            // Read data rows (skip header row)
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

            // Create preview data from Excel
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
            
            // Ensure ColumnMappings exist
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

        // ========== GENERATE NEW UPLOAD ID ==========
        var uploadId = Guid.NewGuid().ToString();
        editedData.UploadId = uploadId;

        // ========== STORE IN CACHE - CLEAR OLD CACHE ==========
        lock (_importCache)
        {
            _logger.LogInformation($"Storing preview data with upload ID: {uploadId}");
            _logger.LogInformation($"Rows count: {editedData.Rows.Count}");
            _logger.LogInformation($"Columns count: {editedData.Columns.Count}");
            
            // Clear ALL old cache entries to prevent stale data
            var oldKeys = _importCache.Keys.ToList();
            _logger.LogInformation($"Clearing {oldKeys.Count} old cache entries");
            
            foreach (var key in oldKeys)
            {
                _logger.LogInformation($"Removing old cache entry: {key}");
                _importCache.Remove(key);
            }
            
            // Store with new ID
            _importCache[uploadId] = editedData;
            _logger.LogInformation($"✅ Cache updated with new upload ID: {uploadId}");
        }

        // ========== RETURN RESPONSE ==========
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

        // ========== FIX: Get preview data from cache for column mappings ONLY ==========
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

        // ========== KEY FIX: Use the rows from the request, NOT from cache ==========
        // The request contains the edited rows - use those!
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

        // Apply mappings
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

        // Log final mappings
        _logger.LogInformation("=== FINAL MAPPINGS ===");
        foreach (var mapping in activeMappings)
        {
            _logger.LogInformation($"  {mapping.ExcelColumn} -> {mapping.DatabaseField}");
        }

        // ========== IMPORT USING THE ROWS FROM THE REQUEST ==========
        var importResult = await ImportExcelData(
            rowsToImport,           // Use rows from request
            previewData.Columns,    // Use columns from cache
            activeMappings,         // Use mappings from cache/request
            request.CompanyId ?? 0, 
            request.OverrideDuplicates);

        // Save import log
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

        [HttpGet("history")]
        public async Task<ActionResult<ApiResponse<List<ExcelImport>>>> GetImportHistory()
        {
            try
            {
                var imports = await _context.ExcelImports
                    .OrderByDescending(i => i.UploadDate)
                    .Take(50)
                    .ToListAsync();

                return Ok(new ApiResponse<List<ExcelImport>>
                {
                    Success = true,
                    Data = imports
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting import history");
                return StatusCode(500, new ApiResponse<List<ExcelImport>>
                {
                    Success = false,
                    Message = $"Error getting import history: {ex.Message}"
                });
            }
        }

        [HttpGet("field-mapping-options")]
        public async Task<ActionResult<ApiResponse<FieldMappingOptions>>> GetFieldMappingOptions()
        {
            try
            {
                var options = new FieldMappingOptions
                {
                    DatabaseFields = new List<FieldOption>
                    {
                        new FieldOption { Field = "Title", Label = "Title (Mr/Mrs/Dr)", Type = "string" },
                        new FieldOption { Field = "Initial", Label = "Initials", Type = "string" },
                        new FieldOption { Field = "FirstName", Label = "First Name *", Type = "string", Required = true },
                        new FieldOption { Field = "LastName", Label = "Last Name *", Type = "string", Required = true },
                        new FieldOption { Field = "SystemName", Label = "System Name *", Type = "string", Required = true },
                        new FieldOption { Field = "NIC", Label = "NIC Number", Type = "string" },
                        new FieldOption { Field = "DOB", Label = "Date of Birth", Type = "date" },
                        new FieldOption { Field = "Gender", Label = "Gender", Type = "string" },
                        new FieldOption { Field = "MaritalStatus", Label = "Marital Status", Type = "string" },
                        new FieldOption { Field = "BloodGroup", Label = "Blood Group", Type = "string" },
                        new FieldOption { Field = "Religion", Label = "Religion", Type = "string" },
                        new FieldOption { Field = "Nationality", Label = "Nationality", Type = "string" },
                        new FieldOption { Field = "Race", Label = "Race", Type = "string" },
                        new FieldOption { Field = "Mobile", Label = "Email/Mobile", Type = "string" },
                        new FieldOption { Field = "LandPhone", Label = "Land Phone", Type = "string" },
                        new FieldOption { Field = "ContactNo", Label = "WhatsApp/Contact No", Type = "string" },
                        new FieldOption { Field = "ResidentialAddress", Label = "Residential Address", Type = "string" },
                        new FieldOption { Field = "PermanentAddress", Label = "Permanent Address", Type = "string" },
                        new FieldOption { Field = "EPFNo", Label = "EPF Number", Type = "string" },
                        new FieldOption { Field = "EmployeeNo", Label = "Employee Number", Type = "string" },
                        new FieldOption { Field = "AttendanceId", Label = "Attendance ID", Type = "string" },
                        new FieldOption { Field = "BasicSalary", Label = "Basic Salary", Type = "decimal" },
                        new FieldOption { Field = "BudgetaryAllowance", Label = "Budget Allowance 1", Type = "decimal" },
                        new FieldOption { Field = "BudgetaryAllowance2", Label = "Budget Allowance 2", Type = "decimal" },
                        new FieldOption { Field = "AttendanceAllowance", Label = "Attendance Allowance", Type = "decimal" },
                        new FieldOption { Field = "FixedAllowance", Label = "Fixed Allowance", Type = "decimal" },
                        new FieldOption { Field = "MealAllowance", Label = "Meal Allowance", Type = "decimal" },
                        new FieldOption { Field = "SpecialAllowance", Label = "Special Allowance", Type = "decimal" },
                        new FieldOption { Field = "TransportAllowance", Label = "Transport Allowance", Type = "decimal" },
                        new FieldOption { Field = "AccountNumber", Label = "Bank Account Number", Type = "string" },
                        new FieldOption { Field = "BankAccountName", Label = "Bank Account Name", Type = "string" },
                        new FieldOption { Field = "BankCode", Label = "Bank Code", Type = "string" },
                        new FieldOption { Field = "BankName", Label = "Bank Name", Type = "string" },
                        new FieldOption { Field = "DesignationId", Label = "Designation", Type = "string" },
                        new FieldOption { Field = "DepartmentId", Label = "Department", Type = "string" },
                        new FieldOption { Field = "ShiftBlockId", Label = "Shift Block", Type = "string" },
                        new FieldOption { Field = "DateOfAppointment", Label = "Date of Appointment", Type = "date" },
                        new FieldOption { Field = "RoleType", Label = "Role Type", Type = "string" },
                        new FieldOption { Field = "Probation", Label = "Probation Status", Type = "bool" },
                        new FieldOption { Field = "Block", Label = "Block Status", Type = "bool" },
                        new FieldOption { Field = "Resigned", Label = "Resigned Status", Type = "bool" },
                        new FieldOption { Field = "ExitType", Label = "Exit Type", Type = "string" },
                        new FieldOption { Field = "ExitReason", Label = "Exit Reason", Type = "string" },
                        new FieldOption { Field = "Keen1ContactName", Label = "Emergency Contact Name", Type = "string" },
                        new FieldOption { Field = "Keen1ContactNumber", Label = "Emergency Contact Number", Type = "string" },
                        new FieldOption { Field = "Keen1Relationship", Label = "Emergency Contact Relationship", Type = "string" },
                        new FieldOption { Field = "Keen1Address", Label = "Emergency Contact Address", Type = "string" },
                        new FieldOption { Field = "Keen2ContactName", Label = "Spouse/2nd Contact Name", Type = "string" },
                        new FieldOption { Field = "Keen2ContactNumber", Label = "Spouse/2nd Contact Number", Type = "string" },
                        new FieldOption { Field = "Keen2Address", Label = "Spouse/2nd Contact Address", Type = "string" },
                        new FieldOption { Field = "Keen2Relationship", Label = "Spouse/2nd Contact Relationship", Type = "string" },
                        new FieldOption { Field = "CompanyId", Label = "Company", Type = "int" }
                    }
                };

                options.RequiredFields = options.DatabaseFields
                    .Where(f => f.Required)
                    .Select(f => f.Field)
                    .ToList();

                return Ok(new ApiResponse<FieldMappingOptions>
                {
                    Success = true,
                    Data = options
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting field mapping options");
                return StatusCode(500, new ApiResponse<FieldMappingOptions>
                {
                    Success = false,
                    Message = $"Error getting field mapping options: {ex.Message}"
                });
            }
        }

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
            // Get EPFNo from the row
            var epfNo = GetStringValue(row, "EPFNo");
            var nic = GetStringValue(row, "NIC");
            
            _logger.LogInformation($"Row {rowNumber}: EPFNo = '{epfNo}', NIC = '{nic}'");

            // ========== CHECK FOR EXISTING EMPLOYEE ==========
            Employee existingEmployee = null;
            List<string> searchCriteria = new List<string>();
            
            if (!string.IsNullOrEmpty(epfNo))
            {
                // Clean EPFNo - remove non-numeric
                var cleanEpf = System.Text.RegularExpressions.Regex.Replace(epfNo, @"[^0-9]", "");
                cleanEpf = cleanEpf.TrimStart('0');
                if (string.IsNullOrEmpty(cleanEpf)) cleanEpf = epfNo;
                
                existingEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EPFNo == cleanEpf && e.CompanyId == companyId);
                searchCriteria.Add($"EPFNo: {cleanEpf}");
                
                // Also check with padded version
                if (existingEmployee == null && cleanEpf.Length < 10)
                {
                    var paddedEpf = cleanEpf.PadLeft(3, '0');
                    if (paddedEpf != cleanEpf)
                    {
                        existingEmployee = await _context.Employees
                            .FirstOrDefaultAsync(e => e.EPFNo == paddedEpf && e.CompanyId == companyId);
                        if (existingEmployee != null)
                        {
                            searchCriteria.Add($"EPFNo (padded): {paddedEpf}");
                        }
                    }
                }
            }
            
            // Check by NIC if not found by EPF
            if (existingEmployee == null && !string.IsNullOrEmpty(nic))
            {
                var cleanedNic = nic.Trim().ToUpper();
                existingEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.NIC == cleanedNic && e.CompanyId == companyId);
                searchCriteria.Add($"NIC: {nic}");
            }

            // ========== HANDLE DUPLICATE ==========
            if (existingEmployee != null)
            {
                _logger.LogInformation($"✅ Found existing employee: ID={existingEmployee.Id}, EPFNo='{existingEmployee.EPFNo}', NIC='{existingEmployee.NIC}'");
                
                if (overrideDuplicates)
                {
                    // ========== UPDATE EXISTING EMPLOYEE ==========
                    _logger.LogInformation($"Updating existing employee: {existingEmployee.Id}");
                    
                    // Update employee from row
                    existingEmployee.Title = GetStringValue(row, "Title") ?? existingEmployee.Title;
                    existingEmployee.Initial = GetStringValue(row, "Initial") ?? existingEmployee.Initial;
                    
                    var firstName = GetStringValue(row, "FirstName");
                    if (!string.IsNullOrEmpty(firstName)) existingEmployee.FirstName = firstName;
                    
                    var lastName = GetStringValue(row, "LastName");
                    if (!string.IsNullOrEmpty(lastName)) existingEmployee.LastName = lastName;
                    
                    var systemName = GetStringValue(row, "SystemName");
                    if (!string.IsNullOrEmpty(systemName)) existingEmployee.SystemName = systemName;
                    
                    var nicValue = GetStringValue(row, "NIC");
                    if (!string.IsNullOrEmpty(nicValue)) existingEmployee.NIC = nicValue;
                    
                    var dob = GetDateTimeValue(row, "DOB");
                    if (dob.HasValue) existingEmployee.DOB = dob;
                    
                    var gender = GetStringValue(row, "Gender");
                    if (!string.IsNullOrEmpty(gender)) existingEmployee.Gender = gender;
                    
                    var maritalStatus = GetStringValue(row, "MaritalStatus");
                    if (!string.IsNullOrEmpty(maritalStatus)) existingEmployee.MaritalStatus = maritalStatus;
                    
                    var bloodGroup = GetStringValue(row, "BloodGroup");
                    if (!string.IsNullOrEmpty(bloodGroup)) existingEmployee.BloodGroup = bloodGroup;
                    
                    var religion = GetStringValue(row, "Religion");
                    if (!string.IsNullOrEmpty(religion)) existingEmployee.Religion = religion;
                    
                    var nationality = GetStringValue(row, "Nationality");
                    if (!string.IsNullOrEmpty(nationality)) existingEmployee.Nationality = nationality;
                    
                    var race = GetStringValue(row, "Race");
                    if (!string.IsNullOrEmpty(race)) existingEmployee.Race = race;
                    
                    var mobile = GetStringValue(row, "Mobile");
                    if (!string.IsNullOrEmpty(mobile)) existingEmployee.Mobile = mobile;
                    
                    var landPhone = GetStringValue(row, "LandPhone");
                    if (!string.IsNullOrEmpty(landPhone)) existingEmployee.LandPhone = landPhone;
                    
                    var contactNo = GetStringValue(row, "ContactNo");
                    if (!string.IsNullOrEmpty(contactNo)) existingEmployee.ContactNo = contactNo;
                    
                    var residentialAddress = GetStringValue(row, "ResidentialAddress");
                    if (!string.IsNullOrEmpty(residentialAddress)) existingEmployee.ResidentialAddress = residentialAddress;
                    
                    var permanentAddress = GetStringValue(row, "PermanentAddress");
                    if (!string.IsNullOrEmpty(permanentAddress)) existingEmployee.PermanentAddress = permanentAddress;
                    
                    // Salary fields
                    if (row.ContainsKey("BasicSalary")) existingEmployee.BasicSalary = GetDecimalValue(row, "BasicSalary");
                    if (row.ContainsKey("BudgetaryAllowance")) existingEmployee.BudgetaryAllowance = GetDecimalValue(row, "BudgetaryAllowance");
                    if (row.ContainsKey("BudgetaryAllowance2")) existingEmployee.BudgetaryAllowance2 = GetDecimalValue(row, "BudgetaryAllowance2");
                    if (row.ContainsKey("AttendanceAllowance")) existingEmployee.AttendanceAllowance = GetDecimalValue(row, "AttendanceAllowance");
                    if (row.ContainsKey("FixedAllowance")) existingEmployee.FixedAllowance = GetDecimalValue(row, "FixedAllowance");
                    if (row.ContainsKey("MealAllowance")) existingEmployee.MealAllowance = GetDecimalValue(row, "MealAllowance");
                    if (row.ContainsKey("SpecialAllowance")) existingEmployee.SpecialAllowance = GetDecimalValue(row, "SpecialAllowance");
                    if (row.ContainsKey("TransportAllowance")) existingEmployee.TransportAllowance = GetDecimalValue(row, "TransportAllowance");
                    
                    // Bank fields
                    var accountNumber = GetStringValue(row, "AccountNumber");
                    if (!string.IsNullOrEmpty(accountNumber)) existingEmployee.AccountNumber = accountNumber;
                    
                    var bankAccountName = GetStringValue(row, "BankAccountName");
                    if (!string.IsNullOrEmpty(bankAccountName)) existingEmployee.BankAccountName = bankAccountName;
                    
                    var bankCode = GetStringValue(row, "BankCode");
                    if (!string.IsNullOrEmpty(bankCode)) existingEmployee.BankCode = bankCode;
                    
                    var bankName = GetStringValue(row, "BankName");
                    if (!string.IsNullOrEmpty(bankName)) existingEmployee.BankName = bankName;
                    
                    // Foreign keys
                    var designationId = GetIntValue(row, "DesignationId");
                    if (designationId.HasValue) existingEmployee.DesignationId = designationId;
                    
                    var departmentId = GetIntValue(row, "DepartmentId");
                    if (departmentId.HasValue) existingEmployee.DepartmentId = departmentId;
                    
                    var shiftBlockId = GetIntValue(row, "ShiftBlockId");
                    if (shiftBlockId.HasValue) existingEmployee.ShiftBlockId = shiftBlockId;
                    
                    // Date fields
                    var dateOfAppointment = GetDateTimeValue(row, "DateOfAppointment");
                    if (dateOfAppointment.HasValue) existingEmployee.DateOfAppointment = dateOfAppointment;
                    
                    // Boolean fields
                    if (row.ContainsKey("EPFPay")) existingEmployee.EPFPay = GetBoolValue(row, "EPFPay");
                    if (row.ContainsKey("Probation")) existingEmployee.Probation = GetBoolValue(row, "Probation");
                    if (row.ContainsKey("Block")) existingEmployee.Block = GetBoolValue(row, "Block");
                    if (row.ContainsKey("Resigned")) existingEmployee.Resigned = GetBoolValue(row, "Resigned");
                    
                    // Emergency Contacts
                    var keen1Name = GetStringValue(row, "Keen1ContactName");
                    if (!string.IsNullOrEmpty(keen1Name)) existingEmployee.Keen1ContactName = keen1Name;
                    
                    var keen1Number = GetStringValue(row, "Keen1ContactNumber");
                    if (!string.IsNullOrEmpty(keen1Number)) existingEmployee.Keen1ContactNumber = keen1Number;
                    
                    var keen1Relationship = GetStringValue(row, "Keen1Relationship");
                    if (!string.IsNullOrEmpty(keen1Relationship)) existingEmployee.Keen1Relationship = keen1Relationship;
                    
                    var keen1Address = GetStringValue(row, "Keen1Address");
                    if (!string.IsNullOrEmpty(keen1Address)) existingEmployee.Keen1Address = keen1Address;
                    
                    var keen2Name = GetStringValue(row, "Keen2ContactName");
                    if (!string.IsNullOrEmpty(keen2Name)) existingEmployee.Keen2ContactName = keen2Name;
                    
                    var keen2Number = GetStringValue(row, "Keen2ContactNumber");
                    if (!string.IsNullOrEmpty(keen2Number)) existingEmployee.Keen2ContactNumber = keen2Number;
                    
                    var keen2Relationship = GetStringValue(row, "Keen2Relationship");
                    if (!string.IsNullOrEmpty(keen2Relationship)) existingEmployee.Keen2Relationship = keen2Relationship;
                    
                    var keen2Address = GetStringValue(row, "Keen2Address");
                    if (!string.IsNullOrEmpty(keen2Address)) existingEmployee.Keen2Address = keen2Address;
                    
                    var roleType = GetStringValue(row, "RoleType");
                    if (!string.IsNullOrEmpty(roleType)) existingEmployee.RoleType = roleType;
                    
                    var exitType = GetStringValue(row, "ExitType");
                    if (!string.IsNullOrEmpty(exitType)) existingEmployee.ExitType = exitType;
                    
                    var exitReason = GetStringValue(row, "ExitReason");
                    if (!string.IsNullOrEmpty(exitReason)) existingEmployee.ExitReason = exitReason;
                    
                    var occupationNo = GetIntValue(row, "OccupationNo");
                    if (occupationNo.HasValue) existingEmployee.OccupationNo = occupationNo;
                    
                    var occupationGrade = GetIntValue(row, "OccupationGrade");
                    if (occupationGrade.HasValue) existingEmployee.OccupationGrade = occupationGrade;
                    
                    existingEmployee.ModifiedDate = DateTime.UtcNow;
                    existingEmployee.ModifiedBy = User.Identity?.Name ?? "System";
                    
                    _context.Employees.Update(existingEmployee);
                    await _context.SaveChangesAsync();
                    
                    await LogEmployeeOperation(existingEmployee.Id, "UPDATE", "Excel Import", row);
                    
                    // ========== ADD TO PROCESSED DATA WITH STATUS ==========
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
                    // ========== SKIP DUPLICATE ==========
                    _logger.LogInformation($"⏭️ Row {rowNumber} skipped (duplicate found)");
                    result.Skipped++;
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = rowNumber,
                        ErrorMessage = $"Duplicate employee found with {string.Join(", ", searchCriteria)} (skipped)",
                        RowData = row.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty)
                    });
                    
                    // ========== ADD TO PROCESSED DATA WITH STATUS ==========
                    var skippedRow = new Dictionary<string, object>(row);
                    skippedRow["_rowNumber"] = rowNumber;
                    skippedRow["_status"] = "skipped";
                    skippedRow["_reason"] = "Duplicate found";
                    skippedRow["_duplicateEmployeeId"] = existingEmployee.Id;
                    skippedRow["_duplicateEPFNo"] = existingEmployee.EPFNo;
                    result.ProcessedData.Add(skippedRow);
                }
                
                // ========== IMPORTANT: Continue to next row ==========
                continue;
            }

            // ========== CREATE NEW EMPLOYEE ==========
            _logger.LogInformation($"✅ No duplicate found. Creating new employee for row {rowNumber}");
            var employee = CreateEmployeeFromRow(row, activeMappings, companyId);
            
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();
            
            var employeeId = employee.Id;
            
            // ========== ADD TO PROCESSED DATA WITH STATUS ==========
            var createdRow = new Dictionary<string, object>(row);
            createdRow["_rowNumber"] = rowNumber;
            createdRow["_status"] = "created";
            createdRow["_employeeId"] = employeeId;
            createdRow["EPFNo"] = employee.EPFNo;
            createdRow["AttendanceId"] = employee.AttendanceId;
            createdRow["EmployeeNo"] = employee.EmployeeNo;
            result.ProcessedData.Add(createdRow);
            
            await LogEmployeeOperation(employeeId, "CREATE", "Excel Import", row);
            
            result.Successful++;
            _logger.LogInformation($"✅ Row {rowNumber} imported successfully");
            _logger.LogInformation($"Employee created: ID={employeeId}, EPFNo='{employee.EPFNo}', SystemName='{employee.SystemName}'");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ Error importing row {rowNumber}");
            
            // ========== ADD ERROR TO PROCESSED DATA WITH STATUS ==========
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
      private Employee CreateEmployeeFromRow(
    Dictionary<string, object> row,
    List<ExcelColumnMapping> mappings,
    int companyId)
{
    _logger.LogInformation("=== CREATING EMPLOYEE FROM ROW ===");
    
    // Log what's actually in the row for debugging
    _logger.LogInformation($"Row keys: {string.Join(", ", row.Keys)}");
    
    var employee = new Employee
    {
        CompanyId = companyId,
        IsActive = true,
        CreatedDate = DateTime.UtcNow,
        ModifiedDate = DateTime.UtcNow,
        CreatedBy = User.Identity?.Name ?? "System",
        ModifiedBy = User.Identity?.Name ?? "System"
    };

    // ========== FIX: Get values directly from the row using DatabaseField names ==========
    // The row already has keys like EPFNo, SystemName, DOB, etc.
    // We don't need to use mappings to look up values - the row is already in the right format!
    
    // ========== GET EPFNo ==========
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
    employee.EPFNo = epfNo;

    // ========== GET SystemName ==========
    var systemName = GetStringValue(row, "SystemName");
    if (string.IsNullOrEmpty(systemName))
    {
        var firstName_ = GetStringValue(row, "FirstName") ?? "";
        var lastName_ = GetStringValue(row, "LastName") ?? "";
        if (!string.IsNullOrEmpty(firstName_) || !string.IsNullOrEmpty(lastName_))
        {
            systemName = (firstName_ + " " + lastName_).Trim();
        }
        if (string.IsNullOrEmpty(systemName))
        {
            systemName = "EMP" + epfNo;
        }
        _logger.LogInformation($"SystemName not provided, generated: {systemName}");
    }
    employee.SystemName = systemName;

    // ========== GET AttendanceId ==========
    var attendanceId = GetStringValue(row, "AttendanceId");
    if (string.IsNullOrEmpty(attendanceId))
    {
        attendanceId = epfNo;
        _logger.LogInformation($"AttendanceId not provided, using EPFNo: {attendanceId}");
    }
    employee.AttendanceId = attendanceId;

    // ========== EmployeeNo ==========
    var employeeNo = GetStringValue(row, "EmployeeNo");
    if (string.IsNullOrEmpty(employeeNo))
    {
        employeeNo = attendanceId;
        _logger.LogInformation($"EmployeeNo not provided, using AttendanceId: {employeeNo}");
    }
    employee.EmployeeNo = employeeNo;

    // ========== FirstName and LastName ==========
    var firstName = GetStringValue(row, "FirstName");
    employee.FirstName = string.IsNullOrEmpty(firstName) ? "" : firstName;
    
    var lastName = GetStringValue(row, "LastName");
    employee.LastName = string.IsNullOrEmpty(lastName) ? "" : lastName;

    // ========== Optional fields - get directly from row ==========
    employee.Title = GetStringValue(row, "Title");
    employee.Initial = GetStringValue(row, "Initial");
    employee.NIC = GetStringValue(row, "NIC");
    employee.DOB = GetDateTimeValue(row, "DOB");
    employee.Gender = GetStringValue(row, "Gender");
    employee.MaritalStatus = GetStringValue(row, "MaritalStatus");
    employee.BloodGroup = GetStringValue(row, "BloodGroup");
    employee.Religion = GetStringValue(row, "Religion");
    employee.Nationality = GetStringValue(row, "Nationality");
    employee.Race = GetStringValue(row, "Race");
    employee.Mobile = GetStringValue(row, "Mobile");
    employee.LandPhone = GetStringValue(row, "LandPhone");
    employee.ContactNo = GetStringValue(row, "ContactNo");
    employee.ResidentialAddress = GetStringValue(row, "ResidentialAddress");
    employee.PermanentAddress = GetStringValue(row, "PermanentAddress");
    
    // Salary fields
    employee.BasicSalary = GetDecimalValue(row, "BasicSalary");
    employee.BudgetaryAllowance = GetDecimalValue(row, "BudgetaryAllowance");
    employee.BudgetaryAllowance2 = GetDecimalValue(row, "BudgetaryAllowance2");
    employee.AttendanceAllowance = GetDecimalValue(row, "AttendanceAllowance");
    employee.FixedAllowance = GetDecimalValue(row, "FixedAllowance");
    employee.MealAllowance = GetDecimalValue(row, "MealAllowance");
    employee.SpecialAllowance = GetDecimalValue(row, "SpecialAllowance");
    employee.TransportAllowance = GetDecimalValue(row, "TransportAllowance");
    
    // Bank fields
    employee.AccountNumber = GetStringValue(row, "AccountNumber");
    employee.BankAccountName = GetStringValue(row, "BankAccountName");
    employee.BankCode = GetStringValue(row, "BankCode");
    employee.BankName = GetStringValue(row, "BankName");
    
    // Date fields
    employee.DateOfAppointment = GetDateTimeValue(row, "DateOfAppointment");
    
    // Foreign keys
    employee.DesignationId = GetIntValue(row, "DesignationId");
    employee.DepartmentId = GetIntValue(row, "DepartmentId");
    employee.ShiftBlockId = GetIntValue(row, "ShiftBlockId");
    
    // Boolean fields
    employee.EPFPay = GetBoolValue(row, "EPFPay");
    employee.Probation = GetBoolValue(row, "Probation");
    employee.Block = GetBoolValue(row, "Block");
    employee.Resigned = GetBoolValue(row, "Resigned");
    
    // Emergency Contacts
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
    employee.ExitType = GetStringValue(row, "ExitType");
    employee.ExitReason = GetStringValue(row, "ExitReason");
    employee.OccupationNo = GetIntValue(row, "OccupationNo");
    employee.OccupationGrade = GetIntValue(row, "OccupationGrade");
    employee.LeaveApproval = GetStringValue(row, "LeaveApproval");
    employee.FinanceApproval = GetStringValue(row, "FinanceApproval");

    _logger.LogInformation($"Employee created: EPFNo='{employee.EPFNo}', AttendanceId='{employee.AttendanceId}', EmployeeNo='{employee.EmployeeNo}'");
    _logger.LogInformation($"FirstName='{employee.FirstName}', LastName='{employee.LastName}', SystemName='{employee.SystemName}'");

    return employee;
}
private void UpdateEmployeeFromRow(
    Employee employee,
    Dictionary<string, object> row,
    List<ExcelColumnMapping> mappings,
    int companyId)
{
    _logger.LogInformation("=== UPDATING EMPLOYEE FROM ROW ===");
    
    // Log what's in the row for debugging
    _logger.LogInformation($"Row keys: {string.Join(", ", row.Keys)}");

    // ========== Get values directly from the row using DatabaseField names ==========
    
    // Update EPFNo if provided
    var epfNo = GetStringValue(row, "EPFNo");
    if (!string.IsNullOrEmpty(epfNo))
    {
        epfNo = System.Text.RegularExpressions.Regex.Replace(epfNo, @"[^0-9]", "");
        epfNo = epfNo.TrimStart('0');
        if (string.IsNullOrEmpty(epfNo)) epfNo = GetStringValue(row, "EPFNo");
        employee.EPFNo = epfNo;
    }

    // Update AttendanceId
    var attendanceId = GetStringValue(row, "AttendanceId");
    if (!string.IsNullOrEmpty(attendanceId))
    {
        employee.AttendanceId = attendanceId;
    }
    else if (string.IsNullOrEmpty(employee.AttendanceId))
    {
        employee.AttendanceId = employee.EPFNo;
    }

    // Update EmployeeNo
    var employeeNo = GetStringValue(row, "EmployeeNo");
    if (!string.IsNullOrEmpty(employeeNo))
    {
        employee.EmployeeNo = employeeNo;
    }
    else if (string.IsNullOrEmpty(employee.EmployeeNo))
    {
        employee.EmployeeNo = !string.IsNullOrEmpty(employee.AttendanceId) 
            ? employee.AttendanceId 
            : employee.EPFNo;
    }

    // Update FirstName and LastName
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

    // Update SystemName
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
        else
        {
            employee.SystemName = "EMP" + employee.EPFNo;
        }
    }

    // ========== Update other fields if provided ==========
    var title = GetStringValue(row, "Title");
    if (!string.IsNullOrEmpty(title)) employee.Title = title;
    
    var initial = GetStringValue(row, "Initial");
    if (!string.IsNullOrEmpty(initial)) employee.Initial = initial;
    
    var nic = GetStringValue(row, "NIC");
    if (!string.IsNullOrEmpty(nic)) employee.NIC = nic;
    
    var dob = GetDateTimeValue(row, "DOB");
    if (dob.HasValue) employee.DOB = dob;
    
    var gender = GetStringValue(row, "Gender");
    if (!string.IsNullOrEmpty(gender)) employee.Gender = gender;
    
    var maritalStatus = GetStringValue(row, "MaritalStatus");
    if (!string.IsNullOrEmpty(maritalStatus)) employee.MaritalStatus = maritalStatus;
    
    var bloodGroup = GetStringValue(row, "BloodGroup");
    if (!string.IsNullOrEmpty(bloodGroup)) employee.BloodGroup = bloodGroup;
    
    var religion = GetStringValue(row, "Religion");
    if (!string.IsNullOrEmpty(religion)) employee.Religion = religion;
    
    var nationality = GetStringValue(row, "Nationality");
    if (!string.IsNullOrEmpty(nationality)) employee.Nationality = nationality;
    
    var race = GetStringValue(row, "Race");
    if (!string.IsNullOrEmpty(race)) employee.Race = race;
    
    var mobile = GetStringValue(row, "Mobile");
    if (!string.IsNullOrEmpty(mobile)) employee.Mobile = mobile;
    
    var landPhone = GetStringValue(row, "LandPhone");
    if (!string.IsNullOrEmpty(landPhone)) employee.LandPhone = landPhone;
    
    var contactNo = GetStringValue(row, "ContactNo");
    if (!string.IsNullOrEmpty(contactNo)) employee.ContactNo = contactNo;
    
    var residentialAddress = GetStringValue(row, "ResidentialAddress");
    if (!string.IsNullOrEmpty(residentialAddress)) employee.ResidentialAddress = residentialAddress;
    
    var permanentAddress = GetStringValue(row, "PermanentAddress");
    if (!string.IsNullOrEmpty(permanentAddress)) employee.PermanentAddress = permanentAddress;
    
    // Salary fields
    if (row.ContainsKey("BasicSalary")) employee.BasicSalary = GetDecimalValue(row, "BasicSalary");
    if (row.ContainsKey("BudgetaryAllowance")) employee.BudgetaryAllowance = GetDecimalValue(row, "BudgetaryAllowance");
    if (row.ContainsKey("BudgetaryAllowance2")) employee.BudgetaryAllowance2 = GetDecimalValue(row, "BudgetaryAllowance2");
    if (row.ContainsKey("AttendanceAllowance")) employee.AttendanceAllowance = GetDecimalValue(row, "AttendanceAllowance");
    if (row.ContainsKey("FixedAllowance")) employee.FixedAllowance = GetDecimalValue(row, "FixedAllowance");
    if (row.ContainsKey("MealAllowance")) employee.MealAllowance = GetDecimalValue(row, "MealAllowance");
    if (row.ContainsKey("SpecialAllowance")) employee.SpecialAllowance = GetDecimalValue(row, "SpecialAllowance");
    if (row.ContainsKey("TransportAllowance")) employee.TransportAllowance = GetDecimalValue(row, "TransportAllowance");
    
    // Bank fields
    var accountNumber = GetStringValue(row, "AccountNumber");
    if (!string.IsNullOrEmpty(accountNumber)) employee.AccountNumber = accountNumber;
    
    var bankAccountName = GetStringValue(row, "BankAccountName");
    if (!string.IsNullOrEmpty(bankAccountName)) employee.BankAccountName = bankAccountName;
    
    var bankCode = GetStringValue(row, "BankCode");
    if (!string.IsNullOrEmpty(bankCode)) employee.BankCode = bankCode;
    
    var bankName = GetStringValue(row, "BankName");
    if (!string.IsNullOrEmpty(bankName)) employee.BankName = bankName;
    
    // Date fields
    var dateOfAppointment = GetDateTimeValue(row, "DateOfAppointment");
    if (dateOfAppointment.HasValue) employee.DateOfAppointment = dateOfAppointment;
    
    // Foreign keys
    var designationId = GetIntValue(row, "DesignationId");
    if (designationId.HasValue) employee.DesignationId = designationId;
    
    var departmentId = GetIntValue(row, "DepartmentId");
    if (departmentId.HasValue) employee.DepartmentId = departmentId;
    
    var shiftBlockId = GetIntValue(row, "ShiftBlockId");
    if (shiftBlockId.HasValue) employee.ShiftBlockId = shiftBlockId;
    
    // Boolean fields
    if (row.ContainsKey("EPFPay")) employee.EPFPay = GetBoolValue(row, "EPFPay");
    if (row.ContainsKey("Probation")) employee.Probation = GetBoolValue(row, "Probation");
    if (row.ContainsKey("Block")) employee.Block = GetBoolValue(row, "Block");
    if (row.ContainsKey("Resigned")) employee.Resigned = GetBoolValue(row, "Resigned");
    
    // Emergency Contacts
    var keen1Name = GetStringValue(row, "Keen1ContactName");
    if (!string.IsNullOrEmpty(keen1Name)) employee.Keen1ContactName = keen1Name;
    
    var keen1Number = GetStringValue(row, "Keen1ContactNumber");
    if (!string.IsNullOrEmpty(keen1Number)) employee.Keen1ContactNumber = keen1Number;
    
    var keen1Relationship = GetStringValue(row, "Keen1Relationship");
    if (!string.IsNullOrEmpty(keen1Relationship)) employee.Keen1Relationship = keen1Relationship;
    
    var keen1Address = GetStringValue(row, "Keen1Address");
    if (!string.IsNullOrEmpty(keen1Address)) employee.Keen1Address = keen1Address;
    
    var keen2Name = GetStringValue(row, "Keen2ContactName");
    if (!string.IsNullOrEmpty(keen2Name)) employee.Keen2ContactName = keen2Name;
    
    var keen2Number = GetStringValue(row, "Keen2ContactNumber");
    if (!string.IsNullOrEmpty(keen2Number)) employee.Keen2ContactNumber = keen2Number;
    
    var keen2Relationship = GetStringValue(row, "Keen2Relationship");
    if (!string.IsNullOrEmpty(keen2Relationship)) employee.Keen2Relationship = keen2Relationship;
    
    var keen2Address = GetStringValue(row, "Keen2Address");
    if (!string.IsNullOrEmpty(keen2Address)) employee.Keen2Address = keen2Address;

    var roleType = GetStringValue(row, "RoleType");
    if (!string.IsNullOrEmpty(roleType)) employee.RoleType = roleType;
    
    var exitType = GetStringValue(row, "ExitType");
    if (!string.IsNullOrEmpty(exitType)) employee.ExitType = exitType;
    
    var exitReason = GetStringValue(row, "ExitReason");
    if (!string.IsNullOrEmpty(exitReason)) employee.ExitReason = exitReason;
    
    var occupationNo = GetIntValue(row, "OccupationNo");
    if (occupationNo.HasValue) employee.OccupationNo = occupationNo;
    
    var occupationGrade = GetIntValue(row, "OccupationGrade");
    if (occupationGrade.HasValue) employee.OccupationGrade = occupationGrade;

    _logger.LogInformation($"Updated employee: EPFNo='{employee.EPFNo}', SystemName='{employee.SystemName}'");
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

       private async Task LogEmployeeOperation(int employeeId, string operation, string source, Dictionary<string, object> rowData)
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
            RowData = JsonSerializer.Serialize(rowData)
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
            // Generate a unique EPF number (8 digits)
            var random = new Random();
            return random.Next(10000000, 99999999).ToString();
        }

        private string GenerateEmployeeNumber()
        {
            var random = new Random();
            return "EMP" + random.Next(100000, 999999).ToString();
        }

        #endregion
    }

    // ============================================================
    // REQUEST/RESPONSE MODELS
    // ============================================================
    public class ConfirmMappingRequest
    {
        public string UploadId { get; set; } = string.Empty;
        public Dictionary<string, string> Mappings { get; set; } = new Dictionary<string, string>();
        public bool OverrideDuplicates { get; set; }
        public int? CompanyId { get; set; }
        public List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>(); 
    }

    public class PreviewOnlyRequest
    {
        public string UploadId { get; set; } = string.Empty;
        public Dictionary<string, string> Mappings { get; set; } = new Dictionary<string, string>();
    }

    public class FieldMappingOptions
    {
        public List<FieldOption> DatabaseFields { get; set; } = new List<FieldOption>();
        public List<string> RequiredFields { get; set; } = new List<string>();
    }

    public class FieldOption
    {
        public string Field { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = "string";
        public bool Required { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }

    // ============================================================
    // AUDIT LOG MODEL
    // ============================================================
    public class EmployeeAuditLog
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string Operation { get; set; } = string.Empty; // "CREATE", "UPDATE", "DELETE"
        public string Source { get; set; } = string.Empty; // "Excel Import", "Manual", "API"
        public string PerformedBy { get; set; } = string.Empty;
        public DateTime PerformedDate { get; set; }
        public string? RowData { get; set; } // JSON data
        public string? Changes { get; set; } // JSON changes
        public string? Notes { get; set; }
    }
}