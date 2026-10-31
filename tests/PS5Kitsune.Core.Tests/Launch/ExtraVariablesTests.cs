using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Core.Tests.Launch;

public sealed class ExtraVariablesTests
{
    private const string BadName = "Line 1: the name must start with APS5_ or ANYPS5_ and use only capital letters, digits and _.";

    [Fact]
    public void Lines_become_variables_and_spaces_and_blank_lines_are_ignored()
    {
        var parse = ExtraVariables.Parse("  APS5_HOST_IMPORT_MIB = 6144  \r\n\r\nANYPS5_TRACE=\n");

        Assert.Empty(parse.Errors);
        Assert.Equal(new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144", ["ANYPS5_TRACE"] = "" }, parse.Variables);
    }

    [Fact]
    public void A_value_may_contain_an_equals_sign() =>
        Assert.Equal("a=b", ExtraVariables.Parse("APS5_X=a=b").Variables["APS5_X"]);

    [Fact]
    public void Empty_text_gives_nothing()
    {
        var parse = ExtraVariables.Parse(null);

        Assert.Empty(parse.Variables);
        Assert.Empty(parse.Errors);
    }

    [Theory]
    [InlineData("APS5_X", "Line 1: write NAME=value.")]
    [InlineData("VK_DRIVER_FILES=x", BadName)]
    [InlineData("aps5_host_import_mib=1", BadName)]
    [InlineData("APS5-HOST=1", BadName)]
    [InlineData("APS5_=1", BadName)]
    [InlineData("ANYPS5_GPU=Radeon", "Line 1: ANYPS5_GPU is set by the GPU setting; use that instead.")]
    [InlineData("ANYPS5_PRESENT_MODE=mailbox", "Line 1: ANYPS5_PRESENT_MODE is set by the vertical sync setting; use that instead.")]
    [InlineData("APS5_OUTPUT_RESOLUTION=1920x1080", "Line 1: APS5_OUTPUT_RESOLUTION is set by the resolution setting; use that instead.")]
    [InlineData("ANYPS5_SYSTEM_FONTS=C:\\fonts", "Line 1: ANYPS5_SYSTEM_FONTS is set by PS5Kitsune itself.")]
    public void A_bad_line_gives_its_error_and_no_variable(string text, string error)
    {
        var parse = ExtraVariables.Parse(text);

        Assert.Equal([new ExtraVariableError(1, error)], parse.Errors);
        Assert.Empty(parse.Variables);
    }

    [Fact]
    public void A_repeated_name_names_its_first_line()
    {
        var parse = ExtraVariables.Parse("APS5_A=1\n\nAPS5_A=2");

        Assert.Equal([new ExtraVariableError(3, "Line 3: APS5_A is already set on line 1.")], parse.Errors);
        Assert.Equal("1", parse.Variables["APS5_A"]);
    }

    [Fact]
    public void Format_sorts_by_name_and_parses_back()
    {
        var variables = new Dictionary<string, string> { ["APS5_B"] = "2", ["ANYPS5_A"] = "x=y" };

        var text = ExtraVariables.Format(variables);

        Assert.Equal("ANYPS5_A=x=y\nAPS5_B=2", text);
        Assert.Equal(variables, ExtraVariables.Parse(text).Variables);
    }
}
