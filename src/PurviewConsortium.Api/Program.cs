using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Identity.Web;
using System.Text;
using System.Text.Json;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PurviewConsortium.Infrastructure;
using PurviewConsortium.Infrastructure.Data;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var isDev = builder.Environment.IsDevelopment();

    var appInsightsConnectionString =
        builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
        ?? builder.Configuration["ApplicationInsights:ConnectionString"];

    // Register AI provider so ASP.NET telemetry (requests/dependencies/exceptions)
    // and ILogger traces can flow to Application Insights.
    if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
    {
        builder.Services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = appInsightsConnectionString;
        });
    }

    // Serilog
    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.Conditional(
            _ => !string.IsNullOrWhiteSpace(appInsightsConnectionString),
            wt => wt.ApplicationInsights(
                services.GetRequiredService<TelemetryConfiguration>(),
                TelemetryConverter.Traces)));

    // Authentication
    var useEntraAuth = builder.Configuration.GetValue<bool>("UseEntraAuth", false);
    if (isDev && !useEntraAuth)
    {
        // Development: use a fake auth handler so the API works without Entra ID
        builder.Services.AddAuthentication("DevScheme")
            .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>("DevScheme", null);
        builder.Services.AddAuthorization();
        Log.Warning("Running in DEVELOPMENT mode — authentication is bypassed with a fake user. Set UseEntraAuth=true to use real Entra ID.");
    }
    else
    {
        builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAd");
        builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var clientId = builder.Configuration["AzureAd:ClientId"];
            if (!string.IsNullOrWhiteSpace(clientId))
            {
                options.TokenValidationParameters.ValidAudiences = new[]
                {
                    clientId,
                    $"api://{clientId}"
                };
            }

            options.Events ??= new JwtBearerEvents();
            options.Events.OnMessageReceived = context =>
            {
                if (isDev && context.Request.Path.StartsWithSegments("/api/requests"))
                {
                    Log.Information(
                        "JWT OnMessageReceived for {Path}. Authorization header present: {HasAuthHeader}",
                        context.Request.Path,
                        context.Request.Headers.ContainsKey("Authorization"));
                }

                return Task.CompletedTask;
            };
            options.Events.OnTokenValidated = context =>
            {
                if (isDev)
                {
                    var principal = context.Principal;
                    var oid = principal?.FindFirst("oid")?.Value
                              ?? principal?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
                    var tid = principal?.FindFirst("tid")?.Value
                              ?? principal?.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
                    var aud = principal?.FindFirst("aud")?.Value;

                    Log.Information(
                        "JWT token validated for {Path}. oid={Oid}, tid={Tid}, aud={Audience}, authenticated={Authenticated}",
                        context.Request.Path,
                        oid ?? "(none)",
                        tid ?? "(none)",
                        aud ?? "(none)",
                        context.Principal?.Identity?.IsAuthenticated == true);
                }

                return Task.CompletedTask;
            };
            options.Events.OnAuthenticationFailed = context =>
            {
                Log.Warning(
                    context.Exception,
                    "JWT authentication failed for {Path}: {Message}",
                    context.Request.Path,
                    context.Exception.Message);

                return Task.CompletedTask;
            };
            options.Events.OnChallenge = context =>
            {
                if (isDev && context.Request.Path.StartsWithSegments("/api/requests"))
                {
                    Log.Warning(
                        "JWT challenge for {Path}. Error={Error}, Description={Description}, Authorization header present: {HasAuthHeader}",
                        context.Request.Path,
                        context.Error ?? "(none)",
                        context.ErrorDescription ?? "(none)",
                        context.Request.Headers.ContainsKey("Authorization"));
                }

                return Task.CompletedTask;
            };
        });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("RequireConsortiumAdmin", p =>
                p.RequireClaim("roles", "Consortium.Admin"))
            .AddPolicy("RequireInstitutionAdmin", p =>
                p.RequireClaim("roles", "Institution.Admin", "Consortium.Admin"));
        if (isDev)
            Log.Information("Running in DEVELOPMENT mode with real Entra ID authentication");
    }

    // Infrastructure layer (DbContext, repositories, services)
    // Check environment variable first (PURVIEW_CONSORTIUM_USE_REAL_DATABASE), then config file, default to false (in-memory)
    var useRealDbEnv = Environment.GetEnvironmentVariable("PURVIEW_CONSORTIUM_USE_REAL_DATABASE");
    var useRealDb = string.IsNullOrEmpty(useRealDbEnv) 
        ? builder.Configuration.GetValue<bool>("UseRealDatabase", false)
        : useRealDbEnv.Equals("true", StringComparison.OrdinalIgnoreCase);
    builder.Services.AddInfrastructure(builder.Configuration, useDevelopmentServices: isDev && !useRealDb);

    // Health checks (verifies SQL / in-memory DB connectivity)
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ConsortiumDbContext>("database", HealthStatus.Unhealthy);

    // Controllers + JSON
    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

    // CORS — allow SPA frontend (configureable via environment variable or appsettings)
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowSPA", policy =>
        {
            // Try environment variable first, then config, then default to localhost
            var originsEnv = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
            string[] origins;
            
            if (!string.IsNullOrEmpty(originsEnv))
            {
                // Parse comma-separated origins from environment variable
                origins = originsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(o => o.Trim())
                    .ToArray();
            }
            else
            {
                origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                    ?? new[] { "http://localhost:5173" };
            }

            var effectiveOrigins = origins.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();
            if (effectiveOrigins.Length == 0)
            {
                // Fallback for misconfigured deployments to keep the API reachable from the SPA.
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
                Log.Warning("CORS_ALLOWED_ORIGINS is empty; using AllowAnyOrigin fallback.");
            }
            else
            {
                policy.WithOrigins(effectiveOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
                Log.Information("CORS enabled for origins: {Origins}", string.Join(", ", effectiveOrigins));
            }
        });
    });

    // Swagger / OpenAPI
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Purview Consortium API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter your Entra ID JWT token"
        });
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    // Database initialization
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ConsortiumDbContext>();
        if (isDev && !useRealDb)
        {
            db.Database.EnsureCreated();
            Log.Information("Development in-memory database seeded");
        }
        else if (!isDev)
        {
            // If the database was previously created before migration history was consolidated
            // into the NewDatabase baseline migration, the tables already exist but the
            // __EFMigrationsHistory table has no record of the new migration ID.
            // Insert the baseline migration record so EF Core skips table creation for
            // tables that are already present, while still applying any future migrations.
            MarkBaselineMigrationIfTablesExist(db);
            db.Database.Migrate();
            Log.Information("Database migrations applied");
        }
        else
        {
            Log.Warning("Using REAL database in Development mode — skipping migrations for safety");

            // Keep local development resilient when the shared dev DB is behind the latest model.
            db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID('dbo.DataProducts', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.DataProducts', 'OwnerContactsJson') IS NULL
        ALTER TABLE dbo.DataProducts ADD OwnerContactsJson nvarchar(max) NULL;

    IF COL_LENGTH('dbo.DataProducts', 'TermsOfUseJson') IS NULL
        ALTER TABLE dbo.DataProducts ADD TermsOfUseJson nvarchar(max) NULL;

    IF COL_LENGTH('dbo.DataProducts', 'DocumentationJson') IS NULL
        ALTER TABLE dbo.DataProducts ADD DocumentationJson nvarchar(max) NULL;
END
");
            Log.Information("Ensured development real-database columns for data product detail JSON fields");
        }
    }

    // Middleware pipeline
    app.UseSerilogRequestLogging();

    if (isDev)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Purview Consortium API v1"));
    }

    app.UseHttpsRedirection();
    app.UseCors("AllowSPA");
    app.UseAuthentication();
    if (isDev)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/requests"))
            {
                Log.Information(
                    "Post-auth middleware for {Path}. Authenticated={Authenticated}, UserName={UserName}, Claims={ClaimCount}",
                    context.Request.Path,
                    context.User?.Identity?.IsAuthenticated == true,
                    context.User?.Identity?.Name ?? "(none)",
                    context.User?.Claims?.Count() ?? 0);
            }

            await next();
        });
    }
    app.UseAuthorization();
    app.MapControllers();

    // Health check endpoint (anonymous, used by Azure App Service)
    app.MapHealthChecks("/healthz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            // Use Utf8JsonWriter to avoid reflection-based metadata traversal in JsonSerializer
            // during health probes on App Service.
            await using var buffer = new MemoryStream();
            await using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("status", report.Status.ToString().ToLowerInvariant());
                writer.WriteString("timestamp", DateTime.UtcNow);
                writer.WriteStartArray("checks");

                foreach (var entry in report.Entries)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", entry.Key);
                    writer.WriteString("status", entry.Value.Status.ToString().ToLowerInvariant());
                    writer.WriteString("description", entry.Value.Description);
                    writer.WriteString("duration", $"{entry.Value.Duration.TotalMilliseconds}ms");
                    writer.WriteString("exception", entry.Value.Exception?.Message);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            await context.Response.WriteAsync(Encoding.UTF8.GetString(buffer.ToArray()));
        }
    }).AllowAnonymous();

    Log.Information("Purview Consortium API starting...");
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// When migrations are consolidated into a single baseline migration, a database that was
/// already created by previous migrations (or EnsureCreated) will have all the tables but
/// no record of the new migration ID in __EFMigrationsHistory.  Calling Migrate() would
/// then try to CREATE TABLE on tables that already exist and throw.
///
/// This helper detects that case and inserts the baseline migration record so EF Core
/// skips the table-creation step and only applies incremental migrations added afterwards.
/// </summary>
static void MarkBaselineMigrationIfTablesExist(ConsortiumDbContext db)
{
    const string baselineMigrationId = "20260313031641_NewDatabase";
    // Retrieve the EF Core version dynamically so the recorded value stays accurate across upgrades.
    var efProductVersion = typeof(DbContext).Assembly.GetName().Version?.ToString(3) ?? "8.0.0";

    // The $ prefix is required: SqlQuery<T> takes FormattableString (the safe, parameterized overload).
    // No interpolated variables means no risk of SQL injection.
    var coreTableCount = db.Database
        .SqlQuery<int>($"SELECT COUNT(1) AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('AuditLogs', 'Institutions', 'DataProducts') AND TABLE_TYPE = 'BASE TABLE'")
        .FirstOrDefault();

    if (coreTableCount < 2)
        return; // Fresh database — let Migrate() build everything from scratch.

    // Use EF Core's IHistoryRepository so the history table schema is always
    // consistent with the version of EF Core in use (avoids manual DDL drift).
    var historyRepo = db.GetService<IHistoryRepository>();

    // Ensure __EFMigrationsHistory exists (may be absent when the DB was built via EnsureCreated).
    db.Database.ExecuteSqlRaw(historyRepo.GetCreateIfNotExistsScript());

    // If the baseline migration is not yet recorded, mark it as applied so that
    // Migrate() skips trying to CREATE TABLE on tables that already exist.
    if (!historyRepo.GetAppliedMigrations().Any(r => r.MigrationId == baselineMigrationId))
    {
        db.Database.ExecuteSqlRaw(
            historyRepo.GetInsertScript(new HistoryRow(baselineMigrationId, efProductVersion)));
        Log.Information(
            "Existing database detected — baseline migration {MigrationId} marked as applied",
            baselineMigrationId);
    }
}

/// <summary>
/// Development-only authentication handler that creates a fake authenticated user.
/// This allows the API to run locally without an Entra ID app registration.
/// </summary>
public class DevAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DevAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "dev-user-001"),
            new Claim("oid", "11111111-1111-1111-1111-111111111111"),
            new Claim("tid", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            new Claim("preferred_username", "dev@contoso-university.edu"),
            new Claim("name", "Dev User"),
            new Claim(ClaimTypes.Email, "dev@contoso-university.edu"),
            new Claim("roles", "Consortium.Admin"),
            new Claim("roles", "Institution.Admin"),
        };

        var identity = new ClaimsIdentity(claims, "DevScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "DevScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
