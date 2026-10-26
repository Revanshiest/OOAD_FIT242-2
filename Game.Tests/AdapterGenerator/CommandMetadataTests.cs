using Game;
using Game.AdapterGenerator;
using Game.Commands;
using Xunit;

namespace Game.Tests;

public class CommandMetadataTests
{
    [Fact]
    public void CommandDependencyKey_FormatsCorrectly()
    {
        var constructor = typeof(MoveCommand).GetConstructors()[0];
        var metadata = new CommandMetadata(typeof(MoveCommand), typeof(IMovingObject), constructor);

        Assert.Equal("Commands.Game_Commands_MoveCommand", metadata.CommandDependencyKey);
    }

    [Fact]
    public void AdapterDependencyKey_FormatsCorrectly()
    {
        var constructor = typeof(MoveCommand).GetConstructors()[0];
        var metadata = new CommandMetadata(typeof(MoveCommand), typeof(IMovingObject), constructor);

        Assert.Equal("Adapters.Game_IMovingObjectAdapter", metadata.AdapterDependencyKey);
    }

    [Fact]
    public void AdapterClassName_FormatsCorrectly()
    {
        var constructor = typeof(MoveCommand).GetConstructors()[0];
        var metadata = new CommandMetadata(typeof(MoveCommand), typeof(IMovingObject), constructor);

        Assert.Equal("Game_IMovingObjectAdapter", metadata.AdapterClassName);
    }
}
