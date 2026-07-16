using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TRIVORA_API.Models;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly string _jwtSecretKey;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;
        private readonly int _jwtExpiryMinutes;

        public AuthController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            
            // ✅ Read from configuration
            _jwtSecretKey = configuration["Jwt:SecretKey"] ?? 
                            throw new Exception("JWT Secret Key not found in configuration!");
            _jwtIssuer = configuration["Jwt:Issuer"] ?? "TRIVORA_API";
            _jwtAudience = configuration["Jwt:Audience"] ?? "TRIVORA_HRIS";
            _jwtExpiryMinutes = int.TryParse(configuration["Jwt:ExpiryMinutes"], out int expiry) ? expiry : 60;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserID) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new { success = false, message = "UserID and Password are required" });
                }

                // Authenticate user
                var user = AuthenticateUser(request.UserID, request.Password);
                if (user == null)
                {
                    return Unauthorized(new { success = false, message = "Invalid credentials" });
                }

                // Get user's companies
                var userCompanies = GetUserCompanies(user.UserID);

                // Generate JWT token
                var jwtToken = GenerateJwtToken(user);

                // Get user settings including inactivity timeout
                var userSettings = GetUserSettingsFromDB(user.UserID);
                var inactivityTimeout = userSettings?.InactivityTimeout ?? 5;

                return Ok(new
                {
                    success = true,
                    message = "Login successful",
                    token = jwtToken,
                    userId = user.UserID,
                    userName = user.UserName,
                    accessType = user.AccessType,
                    systemName = user.SystemName,
                    inactivityTimeout = inactivityTimeout,
                    companies = userCompanies.Select(c => new
                    {
                        id = c.Id,
                        companyName = c.CompanyName,
                        companyAddress = c.CompanyAddress,
                        emailId = c.EmailId,
                        hrManager = c.HrManager,
                        companySector = c.CompanySector
                    }),
                    companyCount = userCompanies.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ✅ Generate JWT token
        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSecretKey);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, user.UserID),
                    new Claim(ClaimTypes.NameIdentifier, user.UserID),
                    new Claim("UserId", user.UserID),
                    new Claim("UserName", user.UserName ?? ""),
                    new Claim("AccessType", user.AccessType ?? "User"),
                    new Claim("SystemName", user.SystemName ?? "")
                }),
                Expires = DateTime.UtcNow.AddMinutes(_jwtExpiryMinutes),
                Issuer = _jwtIssuer,
                Audience = _jwtAudience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        // ✅ Validate JWT token
        [HttpPost("validatetoken")]
        public IActionResult ValidateToken([FromBody] TokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                {
                    return BadRequest(new { success = false, message = "Token is required" });
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_jwtSecretKey);

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(request.Token, tokenValidationParameters, out var validatedToken);
                
                if (validatedToken is JwtSecurityToken jwtToken)
                {
                    var userId = principal.FindFirst("UserId")?.Value ?? 
                                 principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    
                    return Ok(new
                    {
                        valid = true,
                        userId = userId,
                        accessType = principal.FindFirst("AccessType")?.Value,
                        systemName = principal.FindFirst("SystemName")?.Value
                    });
                }

                return Ok(new { valid = false });
            }
            catch (SecurityTokenExpiredException)
            {
                return Ok(new { valid = false, message = "Token expired" });
            }
            catch (Exception ex)
            {
                return Ok(new { valid = false, message = ex.Message });
            }
        }

        [HttpPost("logout")]
        public IActionResult Logout([FromBody] TokenRequest request)
        {
            // JWT is stateless, nothing to invalidate on server side
            // Client should remove the token from storage
            return Ok(new
            {
                success = true,
                message = "Logged out successfully"
            });
        }

        // ============ HELPER METHODS ============

        private User AuthenticateUser(string userID, string password)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"SELECT username, Nic, AccessType, SystemName FROM Admin_Users 
                            WHERE (username = @UserID OR EmailId = @UserID OR NIC = @UserID) 
                            AND (Password = @Password)
                            AND IsActive = 1"; // Use bit/int for boolean

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserID", userID);
                    cmd.Parameters.AddWithValue("@Password", password); // ⚠️ Consider hashing

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new User
                            {
                                UserID = reader["username"].ToString(),
                                UserName = reader["Nic"]?.ToString() ?? "",
                                AccessType = reader["accesstype"]?.ToString() ?? "User",
                                SystemName = reader["SystemName"]?.ToString() ?? ""
                            };
                        }
                    }
                }
            }
            return null;
        }

        private List<Company> GetUserCompanies(string userID)
        {
            var companies = new List<Company>();

            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"
                    SELECT c.* 
                    FROM Company c
                    INNER JOIN Company_Admins ca ON c.Id = ca.CompanyId
                    INNER JOIN Admin_Users au ON ca.AdminId = au.Id
                    WHERE au.username = @UserID OR au.EmailId = @UserID OR au.NIC = @UserID";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserID", userID);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            companies.Add(new Company
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                CompanyName = reader["CompanyName"]?.ToString() ?? string.Empty,
                                CompanyAddress = reader["CompanyAddress"]?.ToString() ?? string.Empty,
                                TelephonNuber = reader["TelephonNuber"]?.ToString() ?? string.Empty,
                                FaxNumber = reader["FaxNumber"]?.ToString() ?? string.Empty,
                                EmailId = reader["EmailId"]?.ToString() ?? string.Empty,
                                HrManager = reader["HrManager"]?.ToString() ?? string.Empty,
                                HrContact = reader["HrContact"]?.ToString() ?? string.Empty,
                                HrEmail = reader["HrEmail"]?.ToString() ?? string.Empty,
                                EpfActNo = reader["EpfActNo"]?.ToString() ?? string.Empty,
                                EtfActNp = reader["EtfActNp"]?.ToString() ?? string.Empty,
                                CompanySector = reader["CompanySector"]?.ToString() ?? string.Empty,
                                CompanyRegistrationNumber = reader["CompanyRegistrationNumber"]?.ToString() ?? string.Empty,
                                CompanyRegisterd = reader["CompanyRegisterd"] as DateTime?,
                                CompanyAbout = reader["CompanyAbout"]?.ToString() ?? string.Empty,
                                MinBasic = reader["MinBasic"] != DBNull.Value ? Convert.ToDouble(reader["MinBasic"]) : 0
                            });
                        }
                    }
                }
            }
            return companies;
        }

        private UserSettings GetUserSettingsFromDB(string userId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"
                    SELECT 
                        Id, 
                        UserName,
                        AccessType,
                        SystemName,
                        CASE 
                            WHEN COL_LENGTH('admin_users', 'InactivityTimeout') IS NOT NULL 
                            THEN ISNULL(InactivityTimeout, 5) 
                            ELSE 5 
                        END as InactivityTimeout
                    FROM admin_users 
                    WHERE Id = @UserId";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new UserSettings
                            {
                                UserID = reader["Id"].ToString(),
                                UserName = reader["UserName"]?.ToString() ?? "",
                                AccessType = reader["AccessType"]?.ToString() ?? "User",
                                SystemName = reader["SystemName"]?.ToString() ?? "",
                                InactivityTimeout = Convert.ToInt32(reader["InactivityTimeout"])
                            };
                        }
                    }
                }
            }
            return null;
        }
    }

    // ============ REQUEST MODELS ============

    public class LoginRequest
    {
        public string UserID { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class TokenRequest
    {
        public string Token { get; set; } = string.Empty;
    }

    public class User
    {
        public string UserID { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string AccessType { get; set; } = string.Empty;
        public string SystemName { get; set; } = string.Empty;
    }

    public class UserSettings
    {
        public string UserID { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string AccessType { get; set; } = string.Empty;
        public string SystemName { get; set; } = string.Empty;
        public int InactivityTimeout { get; set; }
    }

    // ✅ ADD THIS MISSING Company MODEL
   


}