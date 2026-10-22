using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tools;

/// <summary>A portable tool as the Tools screen shows it.</summary>
/// <param name="SizeBytes">Download size from the profile; null when the profile does not give it.</param>
public sealed record PortableToolInfo(string Id, string DisplayName, string Version, long? SizeBytes, bool IsInstalled);

/// <summary>The tools a build-mode install downloads, in install order, with their profile data and whether each is installed.</summary>
public sealed class BuildToolsSummary(AnyPS5Profile profile, IEnumerable<IPortableTool> tools)
{
    private static readonly (string Id, string DisplayName)[] BuildTools =
    [
        (PythonEmbeddableTool.ToolId, "Python"),
        (MinGitTool.ToolId, "MinGit"),
        (WinLibsTool.ToolId, "WinLibs"),
    ];

    /// <summary>Reads the installed state now, with the same check the install steps use. A tool the profile or the container lacks is left out.</summary>
    public IReadOnlyList<PortableToolInfo> Read()
    {
        var registered = tools.ToDictionary(tool => tool.Id);
        return
        [
            .. BuildTools
                .Where(entry => profile.Tools.ContainsKey(entry.Id) && registered.ContainsKey(entry.Id))
                .Select(entry =>
                {
                    var definition = profile.Tools[entry.Id];
                    return new PortableToolInfo(entry.Id, entry.DisplayName, definition.Version, definition.Size, registered[entry.Id].IsInstalled());
                }),
        ];
    }
}
