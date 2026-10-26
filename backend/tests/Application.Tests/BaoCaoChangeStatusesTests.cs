using ThucLuc.Application.Features.BaoCaoBienDong;

namespace ThucLuc.Application.Tests;

public sealed class BaoCaoChangeStatusesTests
{
    [Theory]
    [InlineData(5, 3, BaoCaoMetricDirections.UpIsGood, BaoCaoChangeStatuses.Worsening)]
    [InlineData(3, 5, BaoCaoMetricDirections.UpIsGood, BaoCaoChangeStatuses.Improving)]
    [InlineData(5, 3, BaoCaoMetricDirections.DownIsGood, BaoCaoChangeStatuses.Improving)]
    [InlineData(3, 5, BaoCaoMetricDirections.DownIsGood, BaoCaoChangeStatuses.Worsening)]
    [InlineData(3, 5, BaoCaoMetricDirections.Neutral, BaoCaoChangeStatuses.Changed)]
    [InlineData(5, 5, BaoCaoMetricDirections.UpIsGood, BaoCaoChangeStatuses.Stable)]
    public void Classify_UsesMetricDirection(decimal from, decimal to, string direction, string expected)
    {
        Assert.Equal(expected, BaoCaoChangeStatuses.Classify(from, to, direction));
    }

    [Fact]
    public void Classify_DoesNotCallMissingBaselineAWorseningTrend()
    {
        Assert.Equal(
            BaoCaoChangeStatuses.Missing,
            BaoCaoChangeStatuses.Classify(null, 3, BaoCaoMetricDirections.UpIsGood));
    }
}
