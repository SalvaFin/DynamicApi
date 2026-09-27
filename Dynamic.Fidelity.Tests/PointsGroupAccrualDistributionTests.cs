using Dynamic.Fidelity.Application.Services;
using Xunit;

namespace Dynamic.Fidelity.Tests;

public class PointsGroupAccrualDistributionTests
{
    [Fact]
    public void Split_DistributesAllPointsAndAssignsRemainderInScanOrder()
    {
        int[] result = PointsGroupAccrualDistribution.Split(251, 4).ToArray();

        Assert.Equal(new[] { 63, 63, 63, 62 }, result);
        Assert.Equal(251, result.Sum());
    }

    [Fact]
    public void Split_SupportsSingleRecipientForExistingSingleQrFlow()
        => Assert.Equal(new[] { 80 }, PointsGroupAccrualDistribution.Split(80, 1));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 0)]
    [InlineData(2, -1)]
    public void Split_RejectsIncompleteOrImpossibleDistribution(int points, int recipients)
        => Assert.Throws<ArgumentOutOfRangeException>(() => PointsGroupAccrualDistribution.Split(points, recipients));

    [Fact]
    public void HasUniqueRecipients_RejectsDuplicateOrEmptyQrUsers()
    {
        Guid first = Guid.NewGuid();
        Assert.False(PointsGroupAccrualDistribution.HasUniqueRecipients(new[] { first, first }));
        Assert.False(PointsGroupAccrualDistribution.HasUniqueRecipients(new[] { Guid.Empty }));
        Assert.False(PointsGroupAccrualDistribution.HasUniqueRecipients(Array.Empty<Guid>()));
    }
}
