namespace ProjectMgmt.Tests.Core;

public sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public TestTimeProvider(DateTimeOffset? initialTime = null)
    {
        _utcNow = initialTime ?? new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration)
    {
        _utcNow = _utcNow.Add(duration);
    }

    public void SetUtcNow(DateTimeOffset time)
    {
        _utcNow = time;
    }
}
