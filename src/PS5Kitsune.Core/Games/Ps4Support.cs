namespace PS5Kitsune.Core.Games;

/// <summary>What the active AnyPS5 install offers PS4 games. See the PS4 detection spec.</summary>
/// <param name="DriverPresent">The install's libs folder has <see cref="DriverFile"/>, the PS4 graphics driver.</param>
/// <param name="AnyPS5Name">"AnyPS5 v0.1.4" (the install's version), or "the installed AnyPS5" when none is active.</param>
public sealed record Ps4Support(bool DriverPresent, string AnyPS5Name)
{
    public const string DriverFile = "libSceGnmDriver.prx";

    /// <summary>No active install: the driver is taken as absent.</summary>
    public static Ps4Support Unknown { get; } = new(DriverPresent: false, "the installed AnyPS5");
}
