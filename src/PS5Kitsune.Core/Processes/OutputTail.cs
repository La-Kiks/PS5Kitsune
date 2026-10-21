using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Processes;

/// <summary>Keeps the last lines a tool wrote to its error output, to quote them in an error message.</summary>
public sealed class OutputTail(int capacity = 20)
{
    private readonly Queue<string> _lines = new();
    private readonly Lock _lock = new();

    /// <summary>Pass as the output callback of <see cref="IProcessRunner.RunAsync"/>: keeps the error output only.</summary>
    public void Collect(OutputLine line)
    {
        if (line.Source == OutputSource.StandardError)
        {
            Add(line.Text);
        }
    }

    /// <summary>Keeps a line whatever its source; build tools print compiler errors on the standard output.</summary>
    public void Add(string text)
    {
        lock (_lock)
        {
            _lines.Enqueue(text);
            if (_lines.Count > capacity)
            {
                _lines.Dequeue();
            }
        }
    }

    public override string ToString()
    {
        lock (_lock)
        {
            return string.Join(Environment.NewLine, _lines);
        }
    }
}
