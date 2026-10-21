using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Cli;

/// <summary>How the command's data root and profile were chosen, for the lines 'install' prints first.</summary>
internal sealed record CommandContext(string DataRootSource, ProfileOrigin ProfileOrigin);
