using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PurviewConsortium.Core.Enums;
using PurviewConsortium.Infrastructure.Data;

namespace PurviewConsortium.Api.Controllers;

[ApiController]
[Route("api/admin/health")]
[Authorize]
public class AdminHealthController : ControllerBase
{
    private readonly ConsortiumDbContext _db;
    private readonly ILogger<AdminHealthController> _logger;

    public AdminHealthController(ConsortiumDbContext db, ILogger<AdminHealthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Validates enum-backed database values and returns offending rows.
    /// Useful for diagnosing production issues caused by malformed enum strings.
    /// </summary>
    [HttpGet("invalid-enums")]
    public async Task<ActionResult<object>> GetInvalidEnumValues([FromQuery] int maxRowsPerCheck = 200)
    {
        maxRowsPerCheck = Math.Clamp(maxRowsPerCheck, 1, 2000);

        var checks = new List<InvalidEnumCheckResult>
        {
            await QueryInvalidStringEnumAsync(
                tableName: "AccessRequests",
                idColumn: "Id",
                valueColumn: "Status",
                allowedValues: Enum.GetNames<RequestStatus>(),
                maxRows: maxRowsPerCheck),

            await QueryInvalidIntEnumAsync(
                tableName: "AccessRequests",
                idColumn: "Id",
                valueColumn: "ShareType",
                allowedValues: Enum.GetValues<ShareType>().Cast<int>(),
                maxRows: maxRowsPerCheck),

            await QueryInvalidStringEnumAsync(
                tableName: "SyncHistories",
                idColumn: "Id",
                valueColumn: "Status",
                allowedValues: Enum.GetNames<SyncStatus>(),
                maxRows: maxRowsPerCheck),

            await QueryInvalidStringEnumAsync(
                tableName: "UserRoleAssignments",
                idColumn: "Id",
                valueColumn: "Role",
                allowedValues: Enum.GetNames<UserRole>(),
                maxRows: maxRowsPerCheck),
        };

        var invalidCount = checks.Sum(c => c.InvalidRowCount);
        var result = new
        {
            healthy = invalidCount == 0,
            checkedAtUtc = DateTime.UtcNow,
            invalidRowCount = invalidCount,
            checks,
        };

        if (invalidCount > 0)
        {
            _logger.LogWarning("Admin health enum validation found {InvalidCount} invalid rows.", invalidCount);
        }

        return Ok(result);
    }

    private async Task<InvalidEnumCheckResult> QueryInvalidStringEnumAsync(
        string tableName,
        string idColumn,
        string valueColumn,
        IEnumerable<string> allowedValues,
        int maxRows)
    {
        var allowed = allowedValues.ToArray();
        var allowedList = string.Join(",", allowed.Select(v => $"'{v.Replace("'", "''")}'"));

        var sql = $@"
SELECT TOP ({maxRows})
    CAST([{idColumn}] AS nvarchar(64)) AS RowId,
    CAST([{valueColumn}] AS nvarchar(256)) AS RawValue
FROM [{tableName}]
WHERE [{valueColumn}] IS NULL
   OR LTRIM(RTRIM(CAST([{valueColumn}] AS nvarchar(256)))) = ''
   OR CAST([{valueColumn}] AS nvarchar(256)) NOT IN ({allowedList})
ORDER BY [{idColumn}]";

        var rows = await ExecuteInvalidRowQueryAsync(sql);

        return new InvalidEnumCheckResult
        {
            Table = tableName,
            Column = valueColumn,
            AllowedValues = allowed,
            InvalidRowCount = rows.Count,
            Rows = rows,
        };
    }

    private async Task<InvalidEnumCheckResult> QueryInvalidIntEnumAsync(
        string tableName,
        string idColumn,
        string valueColumn,
        IEnumerable<int> allowedValues,
        int maxRows)
    {
        var allowed = allowedValues.ToArray();
        var allowedList = string.Join(",", allowed);

        var sql = $@"
SELECT TOP ({maxRows})
    CAST([{idColumn}] AS nvarchar(64)) AS RowId,
    CAST([{valueColumn}] AS nvarchar(256)) AS RawValue
FROM [{tableName}]
WHERE [{valueColumn}] IS NULL
   OR [{valueColumn}] NOT IN ({allowedList})
ORDER BY [{idColumn}]";

        var rows = await ExecuteInvalidRowQueryAsync(sql);

        return new InvalidEnumCheckResult
        {
            Table = tableName,
            Column = valueColumn,
            AllowedValues = allowed.Select(v => v.ToString()).ToArray(),
            InvalidRowCount = rows.Count,
            Rows = rows,
        };
    }

    private async Task<List<InvalidRowDto>> ExecuteInvalidRowQueryAsync(string sql)
    {
        var rows = new List<InvalidRowDto>();

        await using var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = System.Data.CommandType.Text;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new InvalidRowDto
            {
                RowId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                RawValue = reader.IsDBNull(1) ? null : reader.GetString(1),
            });
        }

        return rows;
    }

    public class InvalidEnumCheckResult
    {
        public string Table { get; set; } = string.Empty;
        public string Column { get; set; } = string.Empty;
        public string[] AllowedValues { get; set; } = Array.Empty<string>();
        public int InvalidRowCount { get; set; }
        public List<InvalidRowDto> Rows { get; set; } = new();
    }

    public class InvalidRowDto
    {
        public string RowId { get; set; } = string.Empty;
        public string? RawValue { get; set; }
    }
}