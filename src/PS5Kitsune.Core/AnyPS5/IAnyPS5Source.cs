using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>The steps that fill the mode's folder, anyps5\installs\&lt;mode&gt;, and what its install.json says about it.</summary>
public interface IAnyPS5Source
{
    AnyPS5Mode Mode { get; }

    /// <summary>The mode's steps, between "Install Python" and "Record the install".</summary>
    IReadOnlyList<IStep> CreateSteps();

    /// <summary>The record this source's install writes; <paramref name="installedAt"/> is default when comparing.</summary>
    AnyPS5InstallRecord CreateRecord(DateTimeOffset installedAt);
}
