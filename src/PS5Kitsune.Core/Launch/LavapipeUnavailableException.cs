namespace PS5Kitsune.Core.Launch;

/// <summary>The game uses Lavapipe and Mesa cannot be installed: the launch stops. The message is shown as is.</summary>
public sealed class LavapipeUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);
