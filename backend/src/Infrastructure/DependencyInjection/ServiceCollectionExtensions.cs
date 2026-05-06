using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Domain.Entities.Identity;
using ThucLuc.Infrastructure.BackgroundJobs;
using ThucLuc.Infrastructure.Files;
using ThucLuc.Infrastructure.Identity;
using ThucLuc.Infrastructure.Options;
using ThucLuc.Infrastructure.Persistence;
using ThucLuc.Infrastructure.Persistence.HealthChecks;
using ThucLuc.Infrastructure.Persistence.Seeding;
using ThucLuc.Infrastructure.Pdf;
using ThucLuc.Infrastructure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ThucLuc.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<MinioOptions>()
            .Bind(configuration.GetSection(MinioOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<PdfOptions>()
            .Bind(configuration.GetSection(PdfOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<SeedOptions>()
            .Bind(configuration.GetSection(SeedOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<ICacheService, MemoryCacheService>();
        services.AddSingleton<IBackgroundJobService, DisabledBackgroundJobService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();

        var connectionString = configuration.GetConnectionString("Oracle")
            ?? throw new InvalidOperationException("Connection string 'Oracle' is required.");

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var dbOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options
                .UseOracle(connectionString, oracleOptions =>
                {
                    oracleOptions.MigrationsHistoryTable("__EFMigrationsHistory", dbOptions.Schema);
                    oracleOptions.MaxBatchSize(1);
                })
                .UseUpperSnakeCaseNamingConvention()
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });

        services.AddScoped<IApplicationDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());

        services.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            var storageOptions = serviceProvider.GetRequiredService<IOptions<MinioOptions>>().Value;
            var config = new AmazonS3Config
            {
                ServiceURL = storageOptions.ServiceUrl,
                ForcePathStyle = true,
                UseHttp = !storageOptions.UseSsl,
                AuthenticationRegion = storageOptions.Region
            };

            return new AmazonS3Client(new BasicAWSCredentials(storageOptions.AccessKey, storageOptions.SecretKey), config);
        });

        services.AddScoped<IFileStorageService, S3FileStorageService>();
        services.AddScoped<IBaselineDataSeeder, BaselineDataSeeder>();
        services.AddHttpClient<IPdfService, PdfService>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<PdfOptions>>().Value;
            httpClient.BaseAddress = new Uri(options.GotenbergUrl.TrimEnd('/') + "/");
        });

        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Application is running."), tags: ["live"])
            .AddCheck<OracleDbHealthCheck>("oracle-db", failureStatus: HealthStatus.Unhealthy, tags: ["ready"])
            .AddCheck<StorageConfigurationHealthCheck>("storage-configuration", failureStatus: HealthStatus.Unhealthy, tags: ["ready"])
            .AddCheck<PdfConfigurationHealthCheck>("pdf-configuration", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);
        return services;
    }
}