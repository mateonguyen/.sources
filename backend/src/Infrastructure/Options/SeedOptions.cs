namespace ThucLuc.Infrastructure.Options;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool ApplyOnStartup { get; set; }

    public bool ResetBeforeSeed { get; set; }
}