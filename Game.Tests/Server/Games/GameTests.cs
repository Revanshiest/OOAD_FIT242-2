namespace Game.Tests.Server.Games;

using GameServer.Games;
using Game.Tests.Server;
using Game = GameServer.Games.Game;

public class GameTests
{
    private static readonly DateTimeOffset TestTimestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsAwaitingExternalEvent_IsFalse_AfterCreation()
    {
        var game = CreateGame();

        Assert.False(game.IsAwaitingExternalEvent);
        Assert.True(game.CommandQueue.HasCommands == false);
    }

    [Fact]
    public void SuspendForExternalEvent_MarksGameAsAwaiting_FromProvidedTimestamp()
    {
        var game = CreateGame();

        game.SuspendForExternalEvent(TestTimestamp);

        Assert.True(game.IsAwaitingExternalEvent);
        Assert.Equal(TimeSpan.Zero, game.AwaitDuration(TestTimestamp));
    }

    [Fact]
    public void AwaitDuration_MeasuresElapsedDuration_FromSuspensionTimestamp()
    {
        var game = CreateGame();
        game.SuspendForExternalEvent(TestTimestamp);

        var awaitDuration = game.AwaitDuration(TestTimestamp.AddMinutes(6));

        Assert.Equal(TimeSpan.FromMinutes(6), awaitDuration);
    }

    [Fact]
    public void AwaitDuration_ThrowsInvalidOperationException_WhenGameIsNotAwaiting()
    {
        var game = CreateGame();

        Assert.Throws<InvalidOperationException>(() => game.AwaitDuration(TestTimestamp));
    }

    [Fact]
    public void ResumeFromExternalEvent_ReturnsGameToReadyState()
    {
        var game = CreateGame();
        game.SuspendForExternalEvent(TestTimestamp);

        game.ResumeFromExternalEvent();

        Assert.False(game.IsAwaitingExternalEvent);
    }

    [Fact]
    public void ResumeFromExternalEvent_ThrowsInvalidOperationException_WhenGameIsNotAwaiting()
    {
        var game = CreateGame();

        Assert.Throws<InvalidOperationException>(() => game.ResumeFromExternalEvent());
    }

    [Fact]
    public void ExecuteNextCommand_RunsOldestQueuedCommand()
    {
        var game = CreateGame();
        var firstCommand = new RecordingCommand();
        var secondCommand = new RecordingCommand();
        game.CommandQueue.Enqueue(firstCommand);
        game.CommandQueue.Enqueue(secondCommand);

        game.ExecuteNextCommand();

        Assert.True(firstCommand.WasExecuted);
        Assert.False(secondCommand.WasExecuted);
        Assert.Equal(1, game.CommandQueue.Count);
    }

    [Fact]
    public void ExecuteNextCommand_ThrowsInvalidOperationException_WhenQueueIsEmpty()
    {
        var game = CreateGame();

        Assert.Throws<InvalidOperationException>(() => game.ExecuteNextCommand());
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenGameIdIsInvalid()
    {
        Assert.Throws<ArgumentException>(() => new Game("  ", GamePriority.Normal, new Scope()));
    }

    private static Game CreateGame()
    {
        return new Game("game-1", GamePriority.Normal, new Scope());
    }
}
