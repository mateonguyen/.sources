using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog.Context;
using Serilog;
using ThucLuc.Api.Common.Extensions;
using ThucLuc.Api.Common.Filters;
using ThucLuc.Api.Common.Middleware;
using ThucLuc.Application;
using ThucLuc.Infrastructure.DependencyInjection;
using ThucLuc.Infrastructure.Options;
using ThucLuc.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30);
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
    options.Filters.Add<AuditActionFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddPermissionAuthorization();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("refresh", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? throw new InvalidOperationException("JWT configuration is required.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("auth", new OpenApiInfo { Title = "ThucLuc Auth API", Version = "v1" });
    options.SwaggerDoc("snapshot", new OpenApiInfo { Title = "ThucLuc Snapshot API", Version = "v1" });
    options.DocInclusionPredicate((documentName, apiDescription) =>
        string.Equals(apiDescription.GroupName, documentName, StringComparison.OrdinalIgnoreCase));

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT access token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    using (LogContext.PushProperty("CorrelationId", context.TraceIdentifier))
    {
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            using (LogContext.PushProperty("UserId", userId))
            {
                await next();
            }
        }
        else
        {
            await next();
        }
    }
});

var shouldSeedOnStartup = app.Configuration.GetValue<bool>($"{SeedOptions.SectionName}:ApplyOnStartup");
if (shouldSeedOnStartup)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IBaselineDataSeeder>();
    await seeder.SeedAsync();
}

app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/auth/swagger.json", "Auth");
    options.SwaggerEndpoint("/swagger/snapshot/swagger.json", "Snapshot");
});

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", BuildHealthOptions(_ => true)).AllowAnonymous();
app.MapHealthChecks("/health/live", BuildHealthOptions(check => check.Tags.Contains("live", StringComparer.OrdinalIgnoreCase))).AllowAnonymous();
app.MapHealthChecks("/health/ready", BuildHealthOptions(check => check.Tags.Contains("ready", StringComparer.OrdinalIgnoreCase))).AllowAnonymous();

app.MapGet("/version", (IHostEnvironment environment, IConfiguration configuration) =>
{
    var assembly = typeof(Program).Assembly;
    var informationalVersion = assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
        .FirstOrDefault()?.InformationalVersion;

    var metadata = new
    {
        application = configuration["Version:ApplicationName"] ?? "ThucLuc.Api",
        environment = environment.EnvironmentName,
        version = informationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown",
        build = configuration["Version:BuildNumber"] ?? Environment.GetEnvironmentVariable("BUILD_NUMBER") ?? "local",
        commit = configuration["Version:CommitSha"]
                 ?? Environment.GetEnvironmentVariable("BUILD_COMMIT")
                 ?? Environment.GetEnvironmentVariable("GIT_COMMIT")
                 ?? "unknown"
    };

    return Results.Ok(metadata);
}).AllowAnonymous();

app.MapControllers();

app.Run();

static HealthCheckOptions BuildHealthOptions(Func<HealthCheckRegistration, bool> predicate)
{
    return new HealthCheckOptions
    {
        Predicate = predicate,
        ResponseWriter = async (httpContext, report) =>
        {
            var payload = new
            {
                status = report.Status.ToString(),
                totalDurationMs = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        status = kvp.Value.Status.ToString(),
                        description = kvp.Value.Description,
                        durationMs = kvp.Value.Duration.TotalMilliseconds,
                        error = kvp.Value.Exception?.Message
                    })
            };

            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    };
}
