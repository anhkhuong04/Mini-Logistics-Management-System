namespace MiniLogistics.Web.Tests;

internal static class TestClock
{
    public static readonly DateTimeOffset UtcNow = new(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);

    public static TimeProvider Provider { get; } = new FakeTimeProvider(UtcNow);
}

internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }
}
