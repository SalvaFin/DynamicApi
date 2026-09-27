namespace Dynamic.Fidelity.Application.Services;

public static class PointsGroupAccrualDistribution
{
    public static bool HasUniqueRecipients(IReadOnlyCollection<Guid>? recipients, int maximum = 100)
        => recipients is { Count: > 0 } && recipients.Count <= maximum &&
           recipients.All(id => id != Guid.Empty) && recipients.Distinct().Count() == recipients.Count;

    public static IReadOnlyList<int> Split(int totalPoints, int recipientCount)
    {
        if (recipientCount <= 0) throw new ArgumentOutOfRangeException(nameof(recipientCount));
        if (totalPoints < recipientCount) throw new ArgumentOutOfRangeException(nameof(totalPoints), "Cada cliente debe recibir al menos un punto.");
        int baseShare = totalPoints / recipientCount;
        int remainder = totalPoints % recipientCount;
        return Enumerable.Range(0, recipientCount).Select(index => baseShare + (index < remainder ? 1 : 0)).ToArray();
    }
}
