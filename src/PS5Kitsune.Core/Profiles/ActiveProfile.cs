namespace PS5Kitsune.Core.Profiles;

public enum ProfileOrigin
{
    /// <summary>The profile shipped inside the app.</summary>
    Embedded,

    /// <summary>&lt;root&gt;\profiles\override.json.</summary>
    Override,
}

/// <param name="Problem">Why the override was ignored; null when nothing was ignored.</param>
public sealed record ActiveProfile(AnyPS5Profile Profile, ProfileOrigin Origin, string? Problem);
