using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRIVORA_API.Data;
using TRIVORA_API.Models;

namespace TRIVORA_API.Services
{
    public interface IAIExcelMappingService
    {
        Task<List<ExcelColumnMapping>> MapColumnsToDatabaseFields(List<string> excelColumns, List<Dictionary<string, object>> sampleRows);
        Task<Dictionary<string, object>> ValidateAndTransformRow(Dictionary<string, object> row, List<ExcelColumnMapping> mappings);
        Task<List<ImportError>> ValidateAllRows(List<Dictionary<string, object>> rows, List<ExcelColumnMapping> mappings);
    }

    public class AIExcelMappingService : IAIExcelMappingService
    {
        private readonly ILogger<AIExcelMappingService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly Dictionary<string, string> _fieldDescriptions;
        private readonly Dictionary<string, string> _synonyms;

        public AIExcelMappingService(
            ILogger<AIExcelMappingService> logger,
            ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;

            // Define field descriptions for AI matching
            _fieldDescriptions = new Dictionary<string, string>
            {
                // Basic Info
                { "Title", "Employee title (Mr., Mrs., Dr., etc.)" },
                { "Initial", "Initials of the employee" },
                { "FirstName", "Employee's first/given name" },
                { "LastName", "Employee's last/family name" },
                { "SystemName", "Full display name for the employee" },
                { "NIC", "National Identification Card number" },
                { "DOB", "Date of birth" },
                { "Gender", "Gender of the employee" },
                { "MaritalStatus", "Marital status (Single, Married, etc.)" },
                { "BloodGroup", "Blood type of the employee" },
                { "Religion", "Religion of the employee" },
                { "Nationality", "Nationality of the employee" },
                { "Race", "Race of the employee" },
                
                // Contact Info
                { "Mobile", "Mobile phone number" },
                { "LandPhone", "Landline phone number" },
                { "ContactNo", "WhatsApp or primary contact number" },
                { "ResidentialAddress", "Current residential address" },
                { "PermanentAddress", "Permanent address" },
                
                // Employment Info
                { "EPFNo", "Employee Provident Fund number" },
                { "EmployeeNo", "Employee identification number" },
                { "AttendanceId", "Attendance tracking ID" },
                { "DateOfAppointment", "Date of joining/employment" },
                { "RoleType", "Employee role" },
                
                // Salary & Allowances
                { "BasicSalary", "Base salary amount" },
                { "BudgetaryAllowance", "First budget allowance" },
                { "BudgetaryAllowance2", "Second budget allowance" },
                { "AttendanceAllowance", "Attendance-based allowance" },
                { "FixedAllowance", "Fixed monthly allowance" },
                { "MealAllowance", "Meal allowance" },
                { "SpecialAllowance", "Special allowance" },
                { "TransportAllowance", "Transportation allowance" },
                { "AccommodationAllowance", "Accommodation/housing allowance" },
                { "FuelAllowance", "Fuel allowance" },
                { "CostOfLivingAllowance", "Cost of living adjustment" },
                { "PerformanceAllowance", "Performance-based allowance" },
                { "HealthAllowance", "Health insurance allowance" },
                
                // Bank Info
                { "AccountNumber", "Bank account number" },
                { "BankAccountName", "Name on bank account" },
                { "BankCode", "Bank identification code" },
                { "BankName", "Name of the bank" },
                { "BranchCode", "Bank branch code" },
                { "BranchName", "Bank branch name" },
                
                // Foreign Keys
                { "DesignationId", "Job designation/position" },
                { "DepartmentId", "Department name" },
                { "ShiftBlockId", "Shift schedule" },
                { "CompanyId", "Company identifier" },
                
                // Status Fields
                { "Probation", "Is employee on probation (Yes/No)" },
                { "ProbationPeriod", "Length of probation in months" },
                { "ProbationEndDate", "End date of probation" },
                { "Block", "Is employee blocked (Yes/No)" },
                { "BlockUntil", "Block expiry date" },
                { "BlockReason", "Reason for block" },
                { "Resigned", "Has employee resigned (Yes/No)" },
                { "ResignedDate", "Resignation date" },
                { "ExitType", "Type of exit" },
                { "EPFPay", "EPF payment status" },
                
                // Emergency Contacts
                { "Keen1ContactName", "Emergency contact name" },
                { "Keen1ContactNumber", "Emergency contact number" },
                { "Keen1Relationship", "Emergency contact relationship" },
                { "Keen1Address", "Emergency contact address" },
                { "Keen1Position", "Emergency contact position" },
                { "Keen1WorkPlace", "Emergency contact workplace" },
                { "Keen1WorkPlaceContact", "Emergency contact workplace phone" },
                { "Keen2ContactName", "Secondary contact name" },
                { "Keen2ContactNumber", "Secondary contact number" },
                { "Keen2Relationship", "Secondary contact relationship" },
                { "Keen2Address", "Secondary contact address" },
                { "Keen2Position", "Secondary contact position" },
                { "Keen2WorkPlace", "Secondary contact workplace" },
                { "Keen2WorkPlaceContact", "Secondary contact workplace phone" },
                
                // Other
                { "DrivingLicense", "Driving license number" },
                { "OccupationNo", "Occupation number" },
                { "OccupationGrade", "Occupation grade" },
                { "LeaveApproval", "Leave approval authority" },
                { "FinanceApproval", "Finance approval authority" },
                { "SalaryPayType", "Salary payment type" }
            };

            // Define synonyms for field matching
            _synonyms = new Dictionary<string, string>
            {
                // Basic info
                { "first name", "FirstName" },
                { "given name", "FirstName" },
                { "fname", "FirstName" },
                { "firstName", "FirstName" },
                { "first", "FirstName" },
                { "last name", "LastName" },
                { "surname", "LastName" },
                { "lname", "LastName" },
                { "lastName", "LastName" },
                { "last", "LastName" },
                { "full name", "SystemName" },
                { "display name", "SystemName" },
                { "name", "SystemName" },
                { "employee name", "SystemName" },
                { "nic", "NIC" },
                { "national id", "NIC" },
                { "id number", "NIC" },
                { "date of birth", "DOB" },
                { "dob", "DOB" },
                { "birth date", "DOB" },
                { "birthday", "DOB" },
                { "mobile", "Mobile" },
                { "phone", "Mobile" },
                { "contact", "Mobile" },
                { "cell", "Mobile" },
                { "mobileNumber", "Mobile" },
                { "mobilenumber", "Mobile" },
                { "landline", "LandPhone" },
                { "telephone", "LandPhone" },
                { "homeNumber", "LandPhone" },
                { "whatsapp", "ContactNo" },
                { "salary", "BasicSalary" },
                { "basic", "BasicSalary" },
                { "base salary", "BasicSalary" },
                { "Basic", "BasicSalary" },
                { "epf", "EPFNo" },
                { "epf no", "EPFNo" },
                { "epfNo", "EPFNo" },
                { "employee id", "EmployeeNo" },
                { "emp id", "EmployeeNo" },
                { "empid", "EmployeeNo" },
                { "attendance", "AttendanceId" },
                { "attendanceId", "AttendanceId" },
                { "bank account", "AccountNumber" },
                { "account", "AccountNumber" },
                { "acno", "AccountNumber" },
                { "bankNo", "AccountNumber" },
                { "designation", "DesignationId" },
                { "desgination", "DesignationId" },
                { "jobCategory", "DesignationId" },
                { "department", "DepartmentId" },
                { "groupC", "DepartmentId" },
                { "line", "DepartmentId" },
                { "shift", "ShiftBlockId" },
                { "staff", "ShiftBlockId" },
                { "appointment", "DateOfAppointment" },
                { "joining", "DateOfAppointment" },
                { "hire", "DateOfAppointment" },
                { "dateOfAppoinmant", "DateOfAppointment" },
                { "address", "ResidentialAddress" },
                { "home address", "ResidentialAddress" },
                { "residentialAddress", "ResidentialAddress" },
                { "permanent", "PermanentAddress" },
                { "PermanetAddress", "PermanentAddress" },
                { "marital", "MaritalStatus" },
                { "blood", "BloodGroup" },
                { "religion", "Religion" },
                { "nationality", "Nationality" },
                { "race", "Race" },
                { "gender", "Gender" },
                { "title", "Title" },
                { "initials", "Initial" },
                { "ECName", "Keen1ContactName" },
                { "ECAddress", "Keen1Address" },
                { "ECMobileNumber", "Keen1ContactNumber" },
                { "ECRelationShip", "Keen1Relationship" },
                { "SpouseName", "Keen2ContactName" },
                { "SpouseMobileNumber", "Keen2ContactNumber" },
                { "SpouseAddress", "Keen2Address" },
                { "drivingLicence", "DrivingLicense" },
                { "otEpfPay", "EPFPay" },
                { "isBankPay", "SalaryPayType" },
                { "branchNo", "BranchCode" },
                { "budj", "BudgetaryAllowance" },
                { "budgetaryAllowance", "BudgetaryAllowance" },
                { "attendanceAllowance", "AttendanceAllowance" },
                { "resginDate", "ResignedDate" },
                { "BLOCK", "Block" },
                { "RESGIN", "Resigned" },
                { "company", "CompanyId" },
                { "companyId", "CompanyId" },
                { "employer", "CompanyId" },
                { "organization", "CompanyId" }
            };
        }

        public async Task<List<ExcelColumnMapping>> MapColumnsToDatabaseFields(
            List<string> excelColumns,
            List<Dictionary<string, object>> sampleRows)
        {
            _logger.LogInformation($"Mapping {excelColumns.Count} columns");
            _logger.LogInformation($"Columns: {string.Join(", ", excelColumns)}");

            var mappings = new List<ExcelColumnMapping>();
            var usedFields = new HashSet<string>();

            foreach (var column in excelColumns)
            {
                _logger.LogInformation($"Processing column: {column}");

                var match = await FindBestMatch(column, usedFields);

                var mapping = new ExcelColumnMapping
                {
                    ExcelColumn = column,
                    IsMapped = match != null,
                    DatabaseField = match?.FieldName,
                    Confidence = match?.Confidence ?? 0,
                    DataType = match != null ? GetFieldDataType(match.FieldName) : "string"
                };

                if (match != null)
                {
                    usedFields.Add(match.FieldName);
                    _logger.LogInformation($"Mapped '{column}' to '{match.FieldName}' with confidence {match.Confidence}");
                }
                else
                {
                    _logger.LogWarning($"No match found for column '{column}'");
                }

                mappings.Add(mapping);
            }

            _logger.LogInformation($"Returning {mappings.Count} mappings");
            return mappings;
        }

       private async Task<FieldMatch> FindBestMatch(string columnName, HashSet<string> usedFields)
{
    var candidates = new List<FieldMatch>();
    var normalizedColumn = NormalizeString(columnName);
    
    // If the column is just "Column1", "Column2", etc., try to infer from data
    if (columnName.StartsWith("Column", StringComparison.OrdinalIgnoreCase))
    {
        // For generic columns, we'll rely on the data type and position
        // Return null to let user manually map
        return null;
    }

    // Check exact matches first (case insensitive)
    foreach (var field in _fieldDescriptions.Keys)
    {
        if (usedFields.Contains(field)) continue;

        var normalizedField = NormalizeString(field);
        var similarity = CalculateSimilarity(normalizedColumn, normalizedField);

        if (similarity > 0.7)
        {
            candidates.Add(new FieldMatch
            {
                FieldName = field,
                Confidence = similarity,
                Source = "Direct Match"
            });
        }
    }

    // Check synonyms with higher weight
    foreach (var synonym in _synonyms)
    {
        if (usedFields.Contains(synonym.Value)) continue;

        var normalizedSynonym = NormalizeString(synonym.Key);
        var similarity = CalculateSimilarity(normalizedColumn, normalizedSynonym);

        if (similarity > 0.5) // Lowered threshold
        {
            candidates.Add(new FieldMatch
            {
                FieldName = synonym.Value,
                Confidence = similarity * 0.95,
                Source = "Synonym Match"
            });
        }
    }

    // Check field descriptions with partial matching
    foreach (var field in _fieldDescriptions)
    {
        if (usedFields.Contains(field.Key)) continue;

        var normalizedDesc = NormalizeString(field.Value);
        var similarity = CalculateSimilarity(normalizedColumn, normalizedDesc);

        if (similarity > 0.4) // Lowered threshold
        {
            candidates.Add(new FieldMatch
            {
                FieldName = field.Key,
                Confidence = similarity * 0.85,
                Source = "Description Match"
            });
        }
    }

    // If no good match, try to match by data type and position
    if (!candidates.Any())
    {
        // Suggest based on common patterns
        var suggestedField = columnName.ToLower() switch
        {
            var s when s.Contains("name") || s.Contains("full") => "SystemName",
            var s when s.Contains("first") => "FirstName",
            var s when s.Contains("last") || s.Contains("surname") => "LastName",
            var s when s.Contains("email") => "ContactNo",
            var s when s.Contains("phone") || s.Contains("mobile") => "Mobile",
            var s when s.Contains("address") => "ResidentialAddress",
            var s when s.Contains("salary") || s.Contains("basic") => "BasicSalary",
            var s when s.Contains("dob") || s.Contains("birth") => "DOB",
            var s when s.Contains("gender") => "Gender",
            var s when s.Contains("nic") => "NIC",
            var s when s.Contains("epf") => "EPFNo",
            var s when s.Contains("employee") || s.Contains("emp") => "EmployeeNo",
            var s when s.Contains("department") => "DepartmentId",
            var s when s.Contains("designation") => "DesignationId",
            var s when s.Contains("shift") => "ShiftBlockId",
            var s when s.Contains("account") || s.Contains("bank") => "AccountNumber",
            var s when s.Contains("title") => "Title",
            _ => null
        };

        if (suggestedField != null && !usedFields.Contains(suggestedField))
        {
            candidates.Add(new FieldMatch
            {
                FieldName = suggestedField,
                Confidence = 0.6,
                Source = "Pattern Match"
            });
        }
    }

    return candidates.OrderByDescending(c => c.Confidence).FirstOrDefault();
}

        private double CalculateSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0;

            int distance = LevenshteinDistance(s1, s2);
            int maxLength = Math.Max(s1.Length, s2.Length);
            return 1.0 - ((double)distance / maxLength);
        }

        private int LevenshteinDistance(string s1, string s2)
        {
            int[,] dp = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++) dp[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++) dp[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost
                    );
                }
            }

            return dp[s1.Length, s2.Length];
        }

        private string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            return input
                .ToLower()
                .Replace(" ", "")
                .Replace("_", "")
                .Replace("-", "")
                .Replace("/", "")
                .Trim();
        }

        private string GetFieldDataType(string fieldName)
        {
            return fieldName switch
            {
                var f when f.Contains("Salary") || f.Contains("Allowance") || f.Contains("Rate") => "decimal",
                var f when f.Contains("Date") || f.Contains("DOB") || f.EndsWith("Date") => "date",
                var f when f.Contains("Id") || f.Contains("Number") && !f.Contains("Name") => "int",
                var f when f.Contains("No") || f.Contains("ID") => "string",
                var f when f.Contains("Percent") => "decimal",
                var f when f.Contains("Count") || f.Contains("Period") => "int",
                var f when f.Contains("Pay") || f.Contains("Check") || f.Contains("Block") || f.Contains("Resigned") || f.Contains("Probation") => "bool",
                _ => "string"
            };
        }

        public async Task<Dictionary<string, object>> ValidateAndTransformRow(
            Dictionary<string, object> row,
            List<ExcelColumnMapping> mappings)
        {
            var result = new Dictionary<string, object>();
            var errors = new List<string>();

            foreach (var mapping in mappings.Where(m => m.IsMapped && m.DatabaseField != null))
            {
                if (row.TryGetValue(mapping.ExcelColumn, out var value))
                {
                    try
                    {
                        // Special handling for EPFNo
                        if (mapping.DatabaseField == "EPFNo")
                        {
                            if (value == null || string.IsNullOrEmpty(value.ToString()))
                            {
                                value = "EMP" + DateTime.Now.Ticks.ToString().Substring(0, 6);
                            }
                            else
                            {
                                var epfStr = value.ToString();
                                if (!string.IsNullOrEmpty(epfStr))
                                {
                                    epfStr = System.Text.RegularExpressions.Regex.Replace(epfStr, @"[^0-9]", "");
                                }
                                if (string.IsNullOrEmpty(epfStr))
                                {
                                    epfStr = "EMP" + DateTime.Now.Ticks.ToString().Substring(0, 6);
                                }
                                value = epfStr;
                            }
                            result[mapping.DatabaseField] = value;
                            continue;
                        }

                        // Handle CompanyId with proper error handling
                        if (mapping.DatabaseField == "CompanyId")
                        {
                            try
                            {
                                if (value == null || string.IsNullOrEmpty(value.ToString()))
                                {
                                    var defaultCompany = await _context.Companies.FirstOrDefaultAsync();
                                    if (defaultCompany != null)
                                    {
                                        value = defaultCompany.Id;
                                    }
                                    else
                                    {
                                        errors.Add("No CompanyId provided and no default company found.");
                                        continue;
                                    }
                                }
                                else
                                {
                                    var companyIdStr = value.ToString();
                                    if (int.TryParse(companyIdStr, out var companyId))
                                    {
                                        var company = await _context.Companies.FindAsync(companyId);
                                        if (company != null)
                                        {
                                            value = companyId;
                                        }
                                        else
                                        {
                                            errors.Add($"Company with ID '{companyId}' not found.");
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        // Try to find by name
                                        var companyName = companyIdStr.Trim();
                                        var existingCompany = await _context.Companies
                                            .FirstOrDefaultAsync(c => c.CompanyName != null && c.CompanyName.ToLower() == companyName.ToLower());
                                        if (existingCompany != null)
                                        {
                                            value = existingCompany.Id;
                                        }
                                        else
                                        {
                                            errors.Add($"Company '{companyName}' not found.");
                                            continue;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error processing CompanyId from value: {value}");
                                errors.Add($"Error processing CompanyId: {ex.Message}");
                                continue;
                            }
                            result[mapping.DatabaseField] = value;
                            continue;
                        }

                        var transformed = TransformValue(value, mapping.DatabaseField, mapping.DataType);
                        if (transformed != null)
                        {
                            result[mapping.DatabaseField] = transformed;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error mapping {mapping.ExcelColumn} to {mapping.DatabaseField}");
                        errors.Add($"Error mapping {mapping.ExcelColumn} to {mapping.DatabaseField}: {ex.Message}");
                    }
                }
                else
                {
                    // Handle missing fields with defaults
                    if (mapping.DatabaseField == "EPFNo")
                    {
                        result[mapping.DatabaseField] = "EMP" + DateTime.Now.Ticks.ToString().Substring(0, 6);
                    }
                    else if (mapping.DatabaseField == "CompanyId")
                    {
                        try
                        {
                            var defaultCompany = await _context.Companies.FirstOrDefaultAsync();
                            if (defaultCompany != null)
                            {
                                result[mapping.DatabaseField] = defaultCompany.Id;
                            }
                            else
                            {
                                result[mapping.DatabaseField] = 1;
                            }
                        }
                        catch
                        {
                            result[mapping.DatabaseField] = 1;
                        }
                    }
                    else if (mapping.DatabaseField == "SystemName")
                    {
                        result[mapping.DatabaseField] = "Employee " + (result.ContainsKey("EPFNo") ? result["EPFNo"]?.ToString() : DateTime.Now.Ticks.ToString().Substring(0, 6));
                    }
                    else if (mapping.DatabaseField == "FirstName")
                    {
                        result[mapping.DatabaseField] = "Employee";
                    }
                    else if (mapping.DatabaseField == "LastName")
                    {
                        result[mapping.DatabaseField] = "User";
                    }
                }
            }

            // Ensure CompanyId is always present
            if (!result.ContainsKey("CompanyId") || result["CompanyId"] == null)
            {
                try
                {
                    var defaultCompany = await _context.Companies.FirstOrDefaultAsync();
                    result["CompanyId"] = defaultCompany?.Id ?? 1;
                }
                catch
                {
                    result["CompanyId"] = 1;
                }
            }

            // Ensure all required fields exist
            if (!result.ContainsKey("EPFNo") || result["EPFNo"] == null || string.IsNullOrEmpty(result["EPFNo"].ToString()))
            {
                result["EPFNo"] = "EMP" + DateTime.Now.Ticks.ToString().Substring(0, 6);
            }

            if (!result.ContainsKey("SystemName") || result["SystemName"] == null || string.IsNullOrEmpty(result["SystemName"].ToString()))
            {
                var firstName = result.ContainsKey("FirstName") ? result["FirstName"]?.ToString() : "";
                var lastName = result.ContainsKey("LastName") ? result["LastName"]?.ToString() : "";
                if (!string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName))
                {
                    result["SystemName"] = (firstName + " " + lastName).Trim();
                }
                else
                {
                    result["SystemName"] = "Employee " + result["EPFNo"]?.ToString();
                }
            }

            if (!result.ContainsKey("FirstName") || result["FirstName"] == null || string.IsNullOrEmpty(result["FirstName"].ToString()))
            {
                result["FirstName"] = "Employee";
            }
            if (!result.ContainsKey("LastName") || result["LastName"] == null || string.IsNullOrEmpty(result["LastName"].ToString()))
            {
                result["LastName"] = "User";
            }

            result["__validationErrors"] = errors;
            result["__isValid"] = errors.Count == 0;

            return result;
        }

        private object TransformValue(object value, string fieldName, string dataType)
        {
            if (value == null || value == DBNull.Value || string.IsNullOrEmpty(value.ToString()))
                return GetDefaultValue(dataType);

            var stringValue = value.ToString().Trim();

            return dataType switch
            {
                "decimal" => decimal.TryParse(stringValue, out var dec) ? dec : 0,
                "int" => int.TryParse(stringValue, out var num) ? num : 0,
                "date" => DateTime.TryParse(stringValue, out var date) ? date : (DateTime?)null,
                "bool" => IsTrueValue(stringValue),
                _ => stringValue
            };
        }

        private object GetDefaultValue(string dataType)
        {
            return dataType switch
            {
                "decimal" => 0m,
                "int" => 0,
                "date" => (DateTime?)null,
                "bool" => false,
                _ => string.Empty
            };
        }

        private bool IsTrueValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var lower = value.ToLower().Trim();
            return lower == "true" || lower == "1" || lower == "yes" || lower == "y" || lower == "on" || lower == "active";
        }

        public async Task<List<ImportError>> ValidateAllRows(
            List<Dictionary<string, object>> rows,
            List<ExcelColumnMapping> mappings)
        {
            var errors = new List<ImportError>();

            for (int i = 0; i < rows.Count; i++)
            {
                try
                {
                    var row = rows[i];
                    var transformed = await ValidateAndTransformRow(row, mappings);

                    if (transformed.TryGetValue("__validationErrors", out var errorObj))
                    {
                        var errorList = errorObj as List<string>;
                        if (errorList != null && errorList.Count > 0)
                        {
                            errors.Add(new ImportError
                            {
                                RowNumber = i + 2,
                                ErrorMessage = string.Join("; ", errorList),
                                RowData = row.ToDictionary(
                                    kv => kv.Key,
                                    kv => kv.Value?.ToString() ?? string.Empty
                                )
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error validating row {i + 2}");
                    errors.Add(new ImportError
                    {
                        RowNumber = i + 2,
                        ErrorMessage = $"Row validation error: {ex.Message}",
                        RowData = rows[i].ToDictionary(
                            kv => kv.Key,
                            kv => kv.Value?.ToString() ?? string.Empty
                        )
                    });
                }
            }

            return errors;
        }
    }

    public class FieldMatch
    {
        public string FieldName { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string Source { get; set; } = string.Empty;
    }
}