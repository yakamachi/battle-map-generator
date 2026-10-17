namespace BattleMapGenerator.Api.Tests.Infrastructure;

// A clock that only moves when a test moves it, for asserting exact saved-map timestamps.
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
