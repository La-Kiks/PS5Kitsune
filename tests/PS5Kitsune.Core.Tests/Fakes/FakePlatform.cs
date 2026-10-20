using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Tests.Fakes;

internal sealed class FakePlatform : IPlatform
{
    public string Name => "Test";

    public string DefaultDataRoot => "unused";

    public string SettingsDirectory => "unused";

    public string ExecutableSuffix => ".exe";
}
