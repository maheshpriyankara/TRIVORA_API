using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using TRIVORA_API.Models;

namespace TRIVORA_API.Services
{
    public class AttendanceManualLogger
    {
        private readonly string _connectionString;

        public AttendanceManualLogger(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string missing");
        }

        public async Task LogAsync(
            int employeeId,
            string action,
            object? requestData,
            string responseStatus,
            string? errorMessage = null,
            string? ipAddress = null,
            int? userId = null)
        {
            try
            {
                using var con = new SqlConnection(_connectionString);
                await con.ExecuteAsync(
                    @"INSERT INTO AttendanceManualLog 
                        (EmployeeId, Action, RequestData, ResponseStatus, ErrorMessage, [Timestamp], IpAddress, UserId)
                      VALUES 
                        (@EmployeeId, @Action, @RequestData, @ResponseStatus, @ErrorMessage, GETDATE(), @IpAddress, @UserId)",
                    new
                    {
                        EmployeeId = employeeId,
                        Action = action,
                        RequestData = requestData != null
                            ? JsonSerializer.Serialize(requestData)
                            : null,
                        ResponseStatus = responseStatus,
                        ErrorMessage = errorMessage,
                        IpAddress = ipAddress,
                        UserId = userId
                    });
            }
            catch (Exception ex)
            {
                // Never let logging break the main flow
                Console.Error.WriteLine($"[AttendanceManualLog] Failed to log: {ex.Message}");
            }
        }
    }
}