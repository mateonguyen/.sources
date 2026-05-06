using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Infrastructure.Options;
using ThucLuc.Infrastructure.Persistence;
using ThucLuc.Infrastructure.Persistence.Seeding;

namespace ThucLuc.Api.IntegrationTests.Infrastructure;

public sealed class ApiTestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"thuc-luc-tests-{Guid.NewGuid():N}";

    public FakePdfService FakePdfService { get; } = new();

    public FakeFileStorageService FakeFileStorageService { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.Configure<SeedOptions>(options =>
            {
                options.ApplyOnStartup = false;
                options.ResetBeforeSeed = true;
            });

            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<IApplicationDbContext>();
            services.RemoveAll<IFileStorageService>();
            services.RemoveAll<IPdfService>();

            services.AddDbContext<AppDbContext>(options =>
                options
                    .UseInMemoryDatabase(_databaseName)
                    .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));
            services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
            services.AddSingleton<IFileStorageService>(FakeFileStorageService);
            services.AddSingleton<IPdfService>(FakePdfService);
        });
    }

    public async Task InitializeAsync()
    {
        await ResetDataAsync();
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public async Task ResetDataAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<IBaselineDataSeeder>();
        await seeder.SeedAsync();
    }

    public async Task<HttpClient> CreateAuthorizedClientAsync(string username, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username,
            password
        });

        response.EnsureSuccessStatusCode();
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task ExecuteDbContextAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(dbContext);
    }

    public async Task<T> ExecuteDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(dbContext);
    }
}
