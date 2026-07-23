namespace ChatBoard.Api.Tests;

/// <summary>A hand-cranked <see cref="TimeProvider"/> so entity timestamps are deterministic in tests.</summary>
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now;

    public TestClock(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
