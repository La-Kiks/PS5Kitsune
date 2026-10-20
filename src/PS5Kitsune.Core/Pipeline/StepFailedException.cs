namespace PS5Kitsune.Core.Pipeline;

public sealed class StepFailedException(string stepName, Exception innerException)
    : Exception($"{stepName} failed: {innerException.Message}", innerException)
{
    public string StepName { get; } = stepName;
}
