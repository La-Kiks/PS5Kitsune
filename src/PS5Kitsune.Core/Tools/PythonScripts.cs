using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Tools;

/// <summary>How every Python runs a script of AnyPS5.</summary>
public static class PythonScripts
{
    // The ._pth file of the embeddable distribution keeps the script's folder off sys.path and makes
    // Python ignore PYTHONPATH, so a script importing its siblings (import_audit.py imports nid_names
    // and progress) fails when run directly. This bootstrap puts the folder back first, then runs the
    // script as __main__; its SystemExit code becomes the process exit code. A system Python needs no
    // bootstrap, and runs it the same.
    private const string ScriptBootstrap =
        "import os,runpy,sys;sys.argv=sys.argv[1:];sys.path.insert(0,os.path.dirname(os.path.abspath(sys.argv[0])));runpy.run_path(sys.argv[0],run_name='__main__')";

    public static ProcessSpec CreateSpec(string executable, string script, IEnumerable<string> arguments) =>
        new(executable, ["-c", ScriptBootstrap, script, .. arguments]);
}
