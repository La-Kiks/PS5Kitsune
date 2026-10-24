using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Games;

/// <summary>Files of one game folder. See docs/architecture.md, "Root layout".</summary>
public sealed class GameLayout(string root, string titleId, IPlatform platform)
{
    public string Root { get; } = Path.GetFullPath(root);

    public string TitleId { get; } = titleId;

    /// <summary>The game as supplied (eboot.bin unwrapped in place).</summary>
    public string App0 => Path.Combine(Root, "app0");

    public string Libs => Path.Combine(Root, "libs");

    /// <summary>Name of the converted executable, which also names its registry files.</summary>
    public string ExecutableName => TitleId + platform.ExecutableSuffix;

    public string Executable => Path.Combine(Root, ExecutableName);

    public string Registry => Path.Combine(Root, TitleId + ".registry.json");

    public string AuditFile => Path.Combine(Root, "audit.json");

    /// <summary>The app's own folder inside the game, kept out of app0.</summary>
    public string Meta => Path.Combine(Root, ".ps5kitsune");

    /// <summary>Files as they were before unwrapping, at their path relative to app0.</summary>
    public string Originals => Path.Combine(Meta, "originals");

    /// <summary>Copies of the ELF modules only, rebuilt for each audit.</summary>
    public string AuditModules => Path.Combine(Meta, "audit-modules");

    public string Logs => Path.Combine(Meta, "logs");

    /// <summary>Written once the relinker exited 0; its outputs are only trusted with it.</summary>
    public string RelinkedMarker => Path.Combine(Meta, "relinked");

    public string RecordFile => Path.Combine(Meta, "game.json");

    /// <summary>The conversion outputs set aside by a Reconvert that has not finished.</summary>
    public string Previous => Path.Combine(Meta, "previous");

    /// <summary>Outputs being set aside; renamed to <see cref="Previous"/> once all are moved.</summary>
    public string PreviousPartial => Path.Combine(Meta, "previous.partial");

    /// <summary>A set-aside conversion being deleted after a successful Reconvert; never restored.</summary>
    public string PreviousDiscarded => Path.Combine(Meta, "previous.discarded");
}
