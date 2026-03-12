using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace PurviewConsortium.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ConsortiumDbContext>
{
    public ConsortiumDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ConsortiumDbContext>();

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var apiProjectPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "PurviewConsortium.Api"));
        var envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        var environmentSettingsPath = Path.Combine(apiProjectPath, $"appsettings.{environment}.json");
        var baseSettingsPath = Path.Combine(apiProjectPath, "appsettings.json");

        var connectionString = envConnectionString
            ?? TryReadConnectionString(environmentSettingsPath)
            ?? TryReadConnectionString(baseSettingsPath)
            ?? "Server=(localdb)\\mssqllocaldb;Database=PurviewConsortium_Design;Trusted_Connection=True;";

        optionsBuilder.UseSqlServer(connectionString);
        return new ConsortiumDbContext(optionsBuilder.Options);
    }

    private static string? TryReadConnectionString(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(filePath));
        if (!document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings))
        {
            return null;
        }

        if (!connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection))
        {
            return null;
        }

        return defaultConnection.ValueKind == JsonValueKind.String
            ? defaultConnection.GetString()
            : null;
    }
}
