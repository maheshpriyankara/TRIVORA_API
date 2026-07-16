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

                var columnMappings = headers.Select(h => new ExcelColumnMapping
                {
                    ExcelColumn = h,
                    DatabaseField = null,
                    IsMapped = false,
                    Confidence = 0,
                    DataType = "string"
                }).ToList();

                var previewData = new ExcelPreviewData
                {
                    Columns = headers,
                    Rows = allRows,
                    ColumnMappings = columnMappings,
                    TotalRows = allRows.Count,
                    FileName = file.FileName,
                    UploadId = Guid.NewGuid().ToString()
                };

                var uploadId = previewData.UploadId;
                lock (_importCache)
                {
                    _importCache[uploadId] = previewData;
                }

                return Ok(new ApiResponse<ExcelPreviewData>
                {
                    Success = true,
                    Message = $"Excel file processed successfully. Found {allRows.Count} rows and {headers.Count} columns.",
                    Data = previewData
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
                _logger.LogInformation($"Rows count: {previewData.Rows.Count}");

                // Apply mappings
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
                foreach (var mapping in previewData.ColumnMappings)
                {
                    _logger.LogInformation($"  {mapping.ExcelColumn} -> {(mapping.IsMapped ? mapping.DatabaseField : "SKIPPED")}");
                }

                var importResult = await ImportExcelData(previewData, request.CompanyId);

                var importRecord = new ExcelImport
                {
                    FileName = previewData.FileName,
                    UploadDate = DateTime.UtcNow,
                    UploadedBy = User.Identity?.Name ?? "System",
                    Status = importResult.Success ? "Imported" : "Failed",
                    TotalRows = previewData.TotalRows,
                    SuccessfulRows = importResult.Successful,
                    FailedRows = importResult.Failed,
                    MappingData = JsonSerializer.Serialize(previewData.ColumnMappings),
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

        [HttpPost("preview-only")]
        public async Task<ActionResult<ApiResponse<ExcelPreviewData>>> GetPreviewOnly([FromBody] PreviewOnlyRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UploadId))
                    return BadRequest(new ApiResponse<ExcelPreviewData>
                    {
                        Success = false,
                        Message = "Upload ID is required"
                    });

                ExcelPreviewData previewData;
                lock (_importCache)
                {
                    if (!_importCache.TryGetValue(request.UploadId, out previewData))
                        return BadRequest(new ApiResponse<ExcelPreviewData>
                        {
                            Success = false,
                            Message = "Upload session not found"
                        });
                }

                if (request.Mappings != null && request.Mappings.Any())
                {
                    foreach (var mapping in previewData.ColumnMappings)
                    {
                        if (request.Mappings.TryGetValue(mapping.ExcelColumn, out var selectedField))
                        {
                            if (!string.IsNullOrEmpty(selectedField))
                            {
                                mapping.DatabaseField = selectedField;
                                mapping.IsMapped = true;
                                mapping.Confidence = 1.0;
                            }
                            else
                            {
                                mapping.IsMapped = false;
                                mapping.DatabaseField = null;
                            }
                        }
                    }
                }

                return Ok(new ApiResponse<ExcelPreviewData>
                {
                    Success = true,
                    Data = previewData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting preview");
                return StatusCode(500, new ApiResponse<ExcelPreviewData>
                {
                    Success = false,
                    Message = $"Error getting preview: {ex.Message}"
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

        private async Task<ImportResult> ImportExcelData(ExcelPreviewData previewData, int? providedCompanyId = null)
        {
            var result = new ImportResult
            {
                Success = true,
                TotalProcessed = previewData.TotalRows,
                Errors = new List<ImportError>(),
                ProcessedData = new List<Dictionary<string, object>>()
            };

            var companyId = providedCompanyId ?? GetDefaultCompanyId();
            _logger.LogInformation($"Using CompanyId: {companyId}");

            var activeMappings = previewData.ColumnMappings
                .Where(m => m.IsMapped && !string.IsNullOrEmpty(m.DatabaseField))
                .ToList();

            _logger.LogInformation($"Processing {activeMappings.Count} mapped columns");
            foreach (var mapping in activeMappings)
            {
                _logger.LogInformation($"  {mapping.ExcelColumn} -> {mapping.DatabaseField}");
            }

            int rowNumber = 0;
            foreach (var row in previewData.Rows)
            {
                rowNumber++;
                _logger.LogInformation($"=== PROCESSING ROW {rowNumber} ===");
                _logger.LogInformation($"Row data: {JsonSerializer.Serialize(row)}");
                
                try
                {
                    var employee = CreateEmployeeFromRow(row, activeMappings, companyId);
                    _context.Employees.Add(employee);
                    result.Successful++;
                    result.ProcessedData.Add(row);
                    
                    _logger.LogInformation($"✅ Row {rowNumber} imported successfully");
                    _logger.LogInformation($"Employee created: EPFNo='{employee.EPFNo}', SystemName='{employee.SystemName}'");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Error importing row {rowNumber}");
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = rowNumber + 1, // +1 for header
                        ErrorMessage = ex.Message,
                        RowData = row.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty)
                    });
                    result.Failed++;
                }
            }

            if (result.Successful > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Saved {result.Successful} employees to database");
            }

            result.Success = result.Failed == 0;
            return result;
        }

        private Employee CreateEmployeeFromRow(
            Dictionary<string, object> row,
            List<ExcelColumnMapping> mappings,
            int companyId)
        {
            _logger.LogInformation("=== CREATING EMPLOYEE FROM ROW ===");
            
            var employee = new Employee
            {
                CompanyId = companyId,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "System",
                ModifiedBy = User.Identity?.Name ?? "System"
            };

            // Build mapped data from the row using the mappings
            var mappedData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var mapping in mappings)
            {
                if (row.TryGetValue(mapping.ExcelColumn, out var value))
                {
                    _logger.LogInformation($"Mapping: '{mapping.ExcelColumn}' -> '{mapping.DatabaseField}' = '{value}'");
                    mappedData[mapping.DatabaseField] = value;
                }
                else
                {
                    _logger.LogWarning($"Column '{mapping.ExcelColumn}' not found in row");
                }
            }

            // Log all mapped data
            _logger.LogInformation($"Mapped data: {JsonSerializer.Serialize(mappedData)}");

            // Required fields with defaults
            employee.FirstName = GetStringValue(mappedData, "FirstName") ?? "Employee";
            employee.LastName = GetStringValue(mappedData, "LastName") ?? "User";
            employee.SystemName = GetStringValue(mappedData, "SystemName") ?? $"{employee.FirstName} {employee.LastName}";
            employee.EPFNo = GetStringValue(mappedData, "EPFNo") ?? GenerateEPFNumber();
            employee.EmployeeNo = GetStringValue(mappedData, "EmployeeNo") ?? GenerateEmployeeNumber();

            _logger.LogInformation($"EPFNo from data: '{GetStringValue(mappedData, "EPFNo")}'");
            _logger.LogInformation($"SystemName from data: '{GetStringValue(mappedData, "SystemName")}'");
            _logger.LogInformation($"Final EPFNo: '{employee.EPFNo}'");
            _logger.LogInformation($"Final SystemName: '{employee.SystemName}'");

            // Optional fields
            employee.Title = GetStringValue(mappedData, "Title");
            employee.Initial = GetStringValue(mappedData, "Initial");
            employee.NIC = GetStringValue(mappedData, "NIC");
            employee.DOB = GetDateTimeValue(mappedData, "DOB");
            employee.Gender = GetStringValue(mappedData, "Gender");
            employee.MaritalStatus = GetStringValue(mappedData, "MaritalStatus");
            employee.BloodGroup = GetStringValue(mappedData, "BloodGroup");
            employee.Religion = GetStringValue(mappedData, "Religion");
            employee.Nationality = GetStringValue(mappedData, "Nationality");
            employee.Race = GetStringValue(mappedData, "Race");
            employee.Mobile = GetStringValue(mappedData, "Mobile");
            employee.LandPhone = GetStringValue(mappedData, "LandPhone");
            employee.ContactNo = GetStringValue(mappedData, "ContactNo");
            employee.ResidentialAddress = GetStringValue(mappedData, "ResidentialAddress");
            employee.PermanentAddress = GetStringValue(mappedData, "PermanentAddress");
            employee.AttendanceId = GetStringValue(mappedData, "AttendanceId");
            
            // Salary fields
            employee.BasicSalary = GetDecimalValue(mappedData, "BasicSalary");
            employee.BudgetaryAllowance = GetDecimalValue(mappedData, "BudgetaryAllowance");
            employee.BudgetaryAllowance2 = GetDecimalValue(mappedData, "BudgetaryAllowance2");
            employee.AttendanceAllowance = GetDecimalValue(mappedData, "AttendanceAllowance");
            employee.FixedAllowance = GetDecimalValue(mappedData, "FixedAllowance");
            employee.MealAllowance = GetDecimalValue(mappedData, "MealAllowance");
            employee.SpecialAllowance = GetDecimalValue(mappedData, "SpecialAllowance");
            employee.TransportAllowance = GetDecimalValue(mappedData, "TransportAllowance");
            
            // Bank fields
            employee.AccountNumber = GetStringValue(mappedData, "AccountNumber");
            employee.BankAccountName = GetStringValue(mappedData, "BankAccountName");
            employee.BankCode = GetStringValue(mappedData, "BankCode");
            employee.BankName = GetStringValue(mappedData, "BankName");
            
            // Date fields
            employee.DateOfAppointment = GetDateTimeValue(mappedData, "DateOfAppointment");
            
            // Foreign keys
            employee.DesignationId = GetIntValue(mappedData, "DesignationId");
            employee.DepartmentId = GetIntValue(mappedData, "DepartmentId");
            employee.ShiftBlockId = GetIntValue(mappedData, "ShiftBlockId");
            
            // Boolean fields
            employee.EPFPay = GetBoolValue(mappedData, "EPFPay");
            employee.Probation = GetBoolValue(mappedData, "Probation");
            employee.Block = GetBoolValue(mappedData, "Block");
            employee.Resigned = GetBoolValue(mappedData, "Resigned");
            
            // Emergency Contacts
            employee.Keen1ContactName = GetStringValue(mappedData, "Keen1ContactName");
            employee.Keen1ContactNumber = GetStringValue(mappedData, "Keen1ContactNumber");
            employee.Keen1Relationship = GetStringValue(mappedData, "Keen1Relationship");
            employee.Keen1Address = GetStringValue(mappedData, "Keen1Address");
            employee.Keen1Position = GetStringValue(mappedData, "Keen1Position");
            employee.Keen1WorkPlace = GetStringValue(mappedData, "Keen1WorkPlace");
            employee.Keen1WorkPlaceContact = GetStringValue(mappedData, "Keen1WorkPlaceContact");
            
            employee.Keen2ContactName = GetStringValue(mappedData, "Keen2ContactName");
            employee.Keen2ContactNumber = GetStringValue(mappedData, "Keen2ContactNumber");
            employee.Keen2Relationship = GetStringValue(mappedData, "Keen2Relationship");
            employee.Keen2Address = GetStringValue(mappedData, "Keen2Address");
            employee.Keen2Position = GetStringValue(mappedData, "Keen2Position");
            employee.Keen2WorkPlace = GetStringValue(mappedData, "Keen2WorkPlace");
            employee.Keen2WorkPlaceContact = GetStringValue(mappedData, "Keen2WorkPlaceContact");

            // Role Type
            employee.RoleType = GetStringValue(mappedData, "RoleType") ?? "Employee";
            
            // Exit fields
            employee.ExitType = GetStringValue(mappedData, "ExitType");
            employee.ExitReason = GetStringValue(mappedData, "ExitReason");

            // Occupation
            employee.OccupationNo = GetIntValue(mappedData, "OccupationNo");
            employee.OccupationGrade = GetIntValue(mappedData, "OccupationGrade");

            // Leave and Finance Approval
            employee.LeaveApproval = GetStringValue(mappedData, "LeaveApproval");
            employee.FinanceApproval = GetStringValue(mappedData, "FinanceApproval");

            _logger.LogInformation($"=== EMPLOYEE CREATED ===");
            _logger.LogInformation($"EPFNo: '{employee.EPFNo}'");
            _logger.LogInformation($"SystemName: '{employee.SystemName}'");
            _logger.LogInformation($"FirstName: '{employee.FirstName}'");
            _logger.LogInformation($"LastName: '{employee.LastName}'");
            _logger.LogInformation($"Title: '{employee.Title}'");
            _logger.LogInformation($"Gender: '{employee.Gender}'");
            _logger.LogInformation($"DOB: '{employee.DOB}'");
            
            return employee;
        }

        private int GetDefaultCompanyId()
        {
            try
            {
                var company = _context.Companies
                    .Select(c => new { c.Id })
                    .FirstOrDefault();
                
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
                _context.SaveChanges();
                return newCompany.Id;
            }
            catch
            {
                return 1;
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
            return DateTime.Now.Ticks.ToString().Substring(0, 8);
        }

        private string GenerateEmployeeNumber()
        {
            return "EMP" + DateTime.Now.Ticks.ToString().Substring(0, 6);
        }

        private void LogEmployeeData(Employee employee, string context)
        {
            _logger.LogInformation($"=== {context} ===");
            _logger.LogInformation($"CompanyId: {employee.CompanyId}");
            _logger.LogInformation($"EmployeeNo: '{employee.EmployeeNo}'");
            _logger.LogInformation($"EPFNo: '{employee.EPFNo}'");
            _logger.LogInformation($"SystemName: '{employee.SystemName}'");
            _logger.LogInformation($"FirstName: '{employee.FirstName}'");
            _logger.LogInformation($"LastName: '{employee.LastName}'");
            _logger.LogInformation($"Title: '{employee.Title}'");
            _logger.LogInformation($"Gender: '{employee.Gender}'");
            _logger.LogInformation($"DOB: '{employee.DOB}'");
            _logger.LogInformation($"Mobile: '{employee.Mobile}'");
            _logger.LogInformation($"BasicSalary: {employee.BasicSalary}");
            _logger.LogInformation($"DesignationId: {employee.DesignationId}");
            _logger.LogInformation($"DepartmentId: {employee.DepartmentId}");
            _logger.LogInformation("=======================");
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
}