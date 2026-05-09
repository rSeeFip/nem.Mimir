using nem.Mimir.Tui.Commands;
using Shouldly;

namespace nem.Mimir.Tui.Tests.Commands;

public sealed class CommandParserTests
{
    [Fact]
    public void Parse_RegularText_ReturnsChatCommand()
    {
        var result = CommandParser.Parse("hello world");

        result.Type.ShouldBe(CommandType.Chat);
        result.Argument.ShouldBe("hello world");
    }

    [Fact]
    public void Parse_EmptyString_ReturnsChatCommandWithEmptyArgument()
    {
        var result = CommandParser.Parse("");

        result.Type.ShouldBe(CommandType.Chat);
        result.Argument.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsChatCommandWithEmptyArgument()
    {
        var result = CommandParser.Parse("   ");

        result.Type.ShouldBe(CommandType.Chat);
        result.Argument.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_ModelCommand_ReturnsModelTypeWithArgument()
    {
        var result = CommandParser.Parse("/model phi-4-mini");

        result.Type.ShouldBe(CommandType.Model);
        result.Argument.ShouldBe("phi-4-mini");
    }

    [Fact]
    public void Parse_ModelCommandNoArgument_ReturnsModelTypeWithEmptyArgument()
    {
        var result = CommandParser.Parse("/model");

        result.Type.ShouldBe(CommandType.Model);
        result.Argument.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_NewCommand_ReturnsNewTypeWithTitle()
    {
        var result = CommandParser.Parse("/new My Conversation");

        result.Type.ShouldBe(CommandType.New);
        result.Argument.ShouldBe("My Conversation");
    }

    [Fact]
    public void Parse_ListCommand_ReturnsListType()
    {
        var result = CommandParser.Parse("/list");

        result.Type.ShouldBe(CommandType.List);
        result.Argument.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_ArchiveCommand_ReturnsArchiveType()
    {
        var result = CommandParser.Parse("/archive");

        result.Type.ShouldBe(CommandType.Archive);
        result.Argument.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_HelpCommand_ReturnsHelpType()
    {
        var result = CommandParser.Parse("/help");

        result.Type.ShouldBe(CommandType.Help);
        result.Argument.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_QuitCommand_ReturnsQuitType()
    {
        var result = CommandParser.Parse("/quit");

        result.Type.ShouldBe(CommandType.Quit);
        result.Argument.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_ExitCommand_ReturnsQuitType()
    {
        var result = CommandParser.Parse("/exit");

        result.Type.ShouldBe(CommandType.Quit);
        result.Argument.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_UnknownSlashCommand_ReturnsChatWithFullInput()
    {
        var result = CommandParser.Parse("/unknown something");

        result.Type.ShouldBe(CommandType.Chat);
        result.Argument.ShouldBe("/unknown something");
    }

    [Fact]
    public void Parse_CommandWithLeadingWhitespace_TrimsInput()
    {
        var result = CommandParser.Parse("  /help  ");

        result.Type.ShouldBe(CommandType.Help);
        result.Argument.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_CommandIsCaseInsensitive_ReturnsCorrectType()
    {
        var result = CommandParser.Parse("/MODEL phi-4-mini");

        result.Type.ShouldBe(CommandType.Model);
        result.Argument.ShouldBe("phi-4-mini");
    }

    [Fact]
    public void Parse_NewCommandWithExtraSpaces_TrimsArgument()
    {
        var result = CommandParser.Parse("/new   My Title  ");

        result.Type.ShouldBe(CommandType.New);
        result.Argument.ShouldBe("My Title");
    }
}
