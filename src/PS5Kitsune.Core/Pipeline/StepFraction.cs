namespace PS5Kitsune.Core.Pipeline;

/// <summary>Work units done out of a total, for example Ninja's [123/2400].</summary>
public readonly record struct StepFraction(int Done, int Total);
