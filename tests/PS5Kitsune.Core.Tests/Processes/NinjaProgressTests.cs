using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Processes;

namespace PS5Kitsune.Core.Tests.Processes;

public sealed class NinjaProgressTests
{
    [Fact]
    public void Reads_the_counter_of_a_status_line()
    {
        Assert.True(NinjaProgress.TryParse("[123/2400] Building CXX object core/CMakeFiles/core.dir/main.cpp.obj", out var fraction));
        Assert.Equal(new StepFraction(123, 2400), fraction);
    }

    [Theory]
    [InlineData("[0/0] Re-running CMake")]
    [InlineData("[12/abc] nope")]
    [InlineData("[5/3] more done than total")]
    [InlineData("Building CXX object without a counter")]
    [InlineData("  [1/2] indented")]
    [InlineData("")]
    public void Ignores_other_lines(string line)
    {
        Assert.False(NinjaProgress.TryParse(line, out _));
    }
}
