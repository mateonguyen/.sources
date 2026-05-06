using System.ComponentModel.DataAnnotations;

namespace ThucLuc.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string Schema { get; set; } = "CAND_QLCNTT";
}