namespace PS5Kitsune.Core.Tests.Fakes;

/// <summary>A clock stopped at <paramref name="now"/>, in UTC.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}
