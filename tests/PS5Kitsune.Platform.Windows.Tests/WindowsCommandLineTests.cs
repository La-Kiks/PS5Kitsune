namespace PS5Kitsune.Platform.Windows.Tests;

public sealed class WindowsCommandLineTests
{
    [Fact]
    public void Quotes_the_executable_and_leaves_simple_arguments_bare()
    {
        Assert.Equal("\"C:\\Program Files\\cmd.exe\" /c exit 7", WindowsCommandLine.Build(@"C:\Program Files\cmd.exe", ["/c", "exit", "7"]));
    }

    [Fact]
    public void Quotes_and_escapes_the_arguments_that_need_it()
    {
        var commandLine = WindowsCommandLine.Build("x.exe", ["a b", "say \"hi\"", @"dir\", @"C:\a b\", ""]);

        Assert.Equal("\"x.exe\" \"a b\" \"say \\\"hi\\\"\" dir\\ \"C:\\a b\\\\\" \"\"", commandLine);
    }
}
