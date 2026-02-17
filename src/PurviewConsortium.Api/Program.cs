using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using PurviewConsortium.Infrastructure;
using PurviewConsortium.Infrastructure.Data;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var isDev = builder.Environment.IsDevelopment();

    // Serilog
    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    // Authentication
    if (isDev)
    {
        // Development: use a fake auth handler so the API works without Entra ID
        builder.Services.AddAuthentication("DevScheme")
            .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>("DevScheme", null);
        builder.Services.AddAuthorization();
        Log.Warning("Running in DEVELOPMENT mode — authentication is bypassed with a fake user");
    }
    else
    {
        builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAd");
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("RequireConsortiumAdmin", p =>
                p.RequireClaim("roles", "Consortium.Admin"))
            .AddPolicy("RequireInstitutionAdmin", p =>
                p.RequireClaim("roles", "Institution.Admin", "Consortium.Admin"));
    }

    // Infrastructure layer (DbContext, repositories, services)
    builder.Services.AddInfrastructure(builder.Configuration, useDevelopmentServices: isDev);

    // Controllers + JSON
    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

    // CORS — allow SPA dev server
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowSPA", policy =>
        {
            var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { "http://localhost:5173" };
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
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
        if (isDev)
        {
            db.Database.EnsureCreated();
            Log.Information("Development in-memory database seeded");
        }
        else
        {
            // Apply pending migrations to Azure SQL
            db.Database.Migrate();
            Log.Information("Database migrations applied");
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
    app.UseAuthorization();
    app.MapControllers();

    // Health check endpoint (anonymous, used by Azure App Service)
    app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

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
