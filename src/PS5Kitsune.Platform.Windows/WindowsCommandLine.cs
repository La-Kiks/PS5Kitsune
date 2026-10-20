using System.Text;

namespace PS5Kitsune.Platform.Windows;

/// <summary>Builds a CreateProcess command line that the C runtime splits back into the same arguments.</summary>
internal static class WindowsCommandLine
{
    public static string Build(string executable, IReadOnlyList<string> arguments)
    {
        var builder = new StringBuilder().Append('"').Append(executable).Append('"');
        foreach (var argument in arguments)
        {
            builder.Append(' ');
            AppendArgument(builder, argument);
        }

        return builder.ToString();
    }

    // Backslashes are literal except before a quote: 2n backslashes + quote → n backslashes, end of quoting;
    // 2n+1 backslashes + quote → n backslashes and a literal quote.
    private static void AppendArgument(StringBuilder builder, string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '"']) < 0)
        {
            builder.Append(argument);
            return;
        }

        builder.Append('"');
        var backslashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', (backslashes * 2) + 1).Append('"');
            }
            else
            {
                builder.Append('\\', backslashes).Append(character);
            }

            backslashes = 0;
        }

        builder.Append('\\', backslashes * 2).Append('"');
    }
}
