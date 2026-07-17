using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomeController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly string _jwtSecretKey;

        public HomeController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _jwtSecretKey = configuration["Jwt:SecretKey"] ?? 
                            throw new Exception("JWT Secret Key not found in configuration!");
        }

        // ✅ ADD THIS METHOD - This is what's missing!
        [HttpGet("company-settings")]
        public IActionResult GetCompanySettings([FromQuery] string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest(new { success = false, message = "Token is required" });
                }

                // Validate JWT token
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_jwtSecretKey);

                try
                {
                    var validationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = true,
                        ValidIssuer = "TRIVORA_API",
                        ValidateAudience = true,
                        ValidAudience = "TRIVORA_HRIS",
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };

                    var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                    
                    var userIdClaim = principal.FindFirst("UserId") ?? principal.FindFirst(ClaimTypes.NameIdentifier);
                    if (userIdClaim == null)
                    {
                        return Unauthorized(new { success = false, message = "Invalid token claims" });
                    }

                    var userId = userIdClaim.Value;
                    Console.WriteLine($"✅ Token validated for UserId: {userId}");

                    // Get company settings from database
                    var companySettings = GetCompanySettingsFromDB(userId);
                    
                    if (companySettings == null || companySettings.Count == 0)
                    {
                        return Ok(new { 
                            success = false, 
                            message = "Companies not found", 
                            data = new List<CompanySettings>() 
                        });
                    }

                    return Ok(new
                    {
                        success = true,
                        data = companySettings
                    });
                }
                catch (SecurityTokenExpiredException)
                {
                    return Unauthorized(new { success = false, message = "Token has expired" });
                }
                catch (SecurityTokenInvalidSignatureException)
                {
                    return Unauthorized(new { success = false, message = "Invalid token signature" });
                }
                catch (SecurityTokenException ex)
                {
                    return Unauthorized(new { success = false, message = $"Invalid token: {ex.Message}" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetCompanySettings: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("company-departments")]
        public IActionResult GetCompanyDepartments([FromQuery] string companyID)
        {
            try
            {
                if (string.IsNullOrEmpty(companyID))
                {
                    return BadRequest(new { success = false, message = "Company ID is required" });
                }

                var departments = GetDepartmentsFromDB(companyID);
                if (departments == null || departments.Count == 0)
                {
                    return Ok(new { success = false, message = "Departments not found", data = new List<object>() });
                }

                return Ok(new
                {
                    success = true,
                    data = departments
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetCompanyDepartments: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("company-designations")]
        public IActionResult GetCompanyDesignations([FromQuery] string companyID)
        {
            try
            {
                if (string.IsNullOrEmpty(companyID))
                {
                    return BadRequest(new { success = false, message = "Company ID is required" });
                }

                var designations = GetDesignationsFromDB(companyID);
                if (designations == null || designations.Count == 0)
                {
                    return Ok(new { success = false, message = "Designations not found", data = new List<object>() });
                }

                return Ok(new
                {
                    success = true,
                    data = designations
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetCompanyDesignations: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // DATABASE METHODS
        // ============================================
        private List<CompanySettings> GetCompanySettingsFromDB(string userID)
        {
            var companyList = new List<CompanySettings>();

            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"
                    SELECT 
                        c.[Id] AS CompanyId,
                        c.[CompanyName],
                        ISNULL(c.[MinBasic], 0) AS MinBasic,
                        ISNULL(c.[MinBudgetaryAllowanceOne], 0) AS MinBudgetaryAllowanceOne,
                        ISNULL(c.[MinBudgetaryAllowanceTwo], 0) AS MinBudgetaryAllowanceTwo
                    FROM [trivora_hris].[dbo].[Company_Admins] ca
                    INNER JOIN [trivora_hris].[dbo].[Company] c ON ca.[CompanyId] = c.[Id]
                    WHERE ca.[AdminId] = @UserId";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userID);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            companyList.Add(new CompanySettings
                            {
                                CompanyID = reader["CompanyId"].ToString(),
                                CompanyName = reader["CompanyName"].ToString(),
                                MinBasic = Convert.ToDouble(reader["MinBasic"]),
                                MinBudgetaryAllowanceOne = Convert.ToDouble(reader["MinBudgetaryAllowanceOne"]),
                                MinBudgetaryAllowanceTwo = Convert.ToDouble(reader["MinBudgetaryAllowanceTwo"])
                            });
                        }
                    }
                }
            }
            return companyList;
        }

        private List<CompanyDepartments> GetDepartmentsFromDB(string companyID)
        {
            var departmentsList = new List<CompanyDepartments>();

            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"
                    SELECT 
                        [Id],
                        [DepartmentName]
                    FROM [trivora_hris].[dbo].[Company_Departments] 
                    WHERE [CompanyId] = @CompanyId 
                    ORDER BY DepartmentName";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyID);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            departmentsList.Add(new CompanyDepartments
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                DepartmentName = reader["DepartmentName"].ToString()
                            });
                        }
                    }
                }
            }
            return departmentsList;
        }

        private List<CompanyDesignations> GetDesignationsFromDB(string companyID)
        {
            var designationsList = new List<CompanyDesignations>();

            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"
                    SELECT 
                        [Id],
                        [Designation]
                    FROM [trivora_hris].[dbo].[Company_Designations] 
                    WHERE [CompanyId] = @CompanyId 
                    ORDER BY Designation";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyID);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            designationsList.Add(new CompanyDesignations
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                Designation = reader["Designation"].ToString()
                            });
                        }
                    }
                }
            }
            return designationsList;
        }

        // ============================================
        // MODELS
        // ============================================
        public class CompanySettings
        {
            public string CompanyID { get; set; }
            public string CompanyName { get; set; }
            public double MinBasic { get; set; }
            public double MinBudgetaryAllowanceOne { get; set; }
            public double MinBudgetaryAllowanceTwo { get; set; }
        }

        public class CompanyDepartments
        {
            public int Id { get; set; }
            public string DepartmentName { get; set; }
        }

        public class CompanyDesignations
        {
            public int Id { get; set; }
            public string Designation { get; set; }
        }
    }
}