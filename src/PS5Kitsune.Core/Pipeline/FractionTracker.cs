namespace PS5Kitsune.Core.Pipeline;

/// <summary>
/// Keeps one counter per command. A sub-build that runs its own Ninja prints a smaller "[n/m]" between the main
/// counter's lines, so the counter with the largest total wins and a counter with a smaller total is ignored.
/// </summary>
public sealed class FractionTracker
{
    private int _total;

    /// <returns>True when <paramref name="fraction"/> belongs to the counter being followed.</returns>
    public bool TryAccept(StepFraction fraction)
    {
        if (fraction.Total < _total)
        {
            return false;
        }

        _total = fraction.Total;
        return true;
    }
}
