using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Tests.Pipeline;

public sealed class FractionTrackerTests
{
    [Fact]
    public void Accepts_the_first_counter()
    {
        Assert.True(new FractionTracker().TryAccept(new StepFraction(1, 10)));
    }

    [Fact]
    public void Ignores_a_counter_with_a_smaller_total()
    {
        var tracker = new FractionTracker();
        tracker.TryAccept(new StepFraction(1, 10));

        Assert.False(tracker.TryAccept(new StepFraction(2, 3)));
        Assert.True(tracker.TryAccept(new StepFraction(2, 10)));
    }

    [Fact]
    public void A_larger_total_takes_over()
    {
        var tracker = new FractionTracker();
        tracker.TryAccept(new StepFraction(1, 10));

        Assert.True(tracker.TryAccept(new StepFraction(1, 20)));
        Assert.False(tracker.TryAccept(new StepFraction(3, 10)));
    }
}
