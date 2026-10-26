namespace ThucLuc.Infrastructure.Options;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool ApplyOnStartup { get; set; }

    public bool ResetBeforeSeed { get; set; }

    /// <summary>
    /// Seed du lieu demo (1 mau bao cao day du module, 4 ky quy, 10 don vi
    /// that + tai khoan test) de QA co du lieu thuc te tren nhieu man hinh.
    /// Tu bo qua neu da chay 1 lan (kiem tra MA_MAU = DEMO_FULL).
    /// </summary>
    public bool SeedDemoReportingData { get; set; }
}