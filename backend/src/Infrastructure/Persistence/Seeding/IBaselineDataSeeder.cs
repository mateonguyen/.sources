namespace ThucLuc.Infrastructure.Persistence.Seeding;

public interface IBaselineDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}