namespace DeliveryIntelligence.Domain.UseCases;

/// <summary>
/// RankOrder kiểu DECIMAL(30,15): chèn giữa hai Issue = trung bình cộng hai rank kề nhau.
/// Khi khoảng cách nhỏ hơn <see cref="MinGap"/> thì cần rebalance cả Project trước.
/// </summary>
public static class IssueRanking
{
    public const int Scale = 15;
    public static readonly decimal MinGap = 0.000000001m;

    /// <returns>Rank mới, hoặc null nếu hai rank kề quá sát (cần rebalance).</returns>
    public static decimal? Between(decimal? previousRank, decimal? nextRank)
    {
        if (previousRank is null && nextRank is null)
        {
            throw new ArgumentException("Cần ít nhất một Issue kề để xếp hạng.");
        }

        if (previousRank is null)
        {
            return nextRank!.Value - 1;
        }

        if (nextRank is null)
        {
            return previousRank.Value + 1;
        }

        if (nextRank.Value - previousRank.Value < MinGap)
        {
            return null;
        }

        return Math.Round((previousRank.Value + nextRank.Value) / 2, Scale, MidpointRounding.ToZero);
    }

    /// <summary>Rank sau rebalance: 1, 2, 3… theo thứ tự hiện có.</summary>
    public static decimal Rebalanced(int position) => position + 1;
}
