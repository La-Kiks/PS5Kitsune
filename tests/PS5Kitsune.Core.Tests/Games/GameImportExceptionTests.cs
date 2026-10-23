using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class GameImportExceptionTests
{
    [Fact]
    public void Without_details_the_message_is_the_summary()
    {
        var exception = new GameImportException("It failed.");

        Assert.Equal("It failed.", exception.Message);
        Assert.Null(exception.Details);
    }

    [Fact]
    public void With_details_the_message_joins_them_on_a_new_line()
    {
        var exception = new GameImportException("It failed.", "line 1");

        Assert.Equal("It failed." + Environment.NewLine + "line 1", exception.Message);
        Assert.Equal("It failed.", exception.Summary);
        Assert.Equal("line 1", exception.Details);
    }
}
