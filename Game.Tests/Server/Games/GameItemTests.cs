using GameServer.Games;

namespace Game.Tests.Server.Games;

public class GameItemTests
{
    [Fact]
    public void SetProperty_And_GetProperty_ReturnStoredValue()
    {
        var item = new GameItem();

        item.SetProperty("Position", new[] { 1, 2, 3 });

        Assert.Equal(new[] { 1, 2, 3 }, item.GetProperty("Position"));
    }

    [Fact]
    public void SetProperty_OverwritesPreviousValue()
    {
        var item = new GameItem();

        item.SetProperty("Position", new[] { 1, 2, 3 });
        item.SetProperty("Position", new[] { 4, 5, 6 });

        Assert.Equal(new[] { 4, 5, 6 }, item.GetProperty("Position"));
    }

    [Fact]
    public void GetProperty_ThrowsKeyNotFoundException_WhenPropertyWasNotSet()
    {
        var item = new GameItem();

        Assert.Throws<KeyNotFoundException>(() => item.GetProperty("Speed"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetProperty_ThrowsArgumentException_WhenPropertyNameIsInvalid(string? propertyName)
    {
        var item = new GameItem();

        Assert.Throws<ArgumentException>(() => item.GetProperty(propertyName!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetProperty_ThrowsArgumentException_WhenPropertyNameIsInvalid(string? propertyName)
    {
        var item = new GameItem();

        Assert.Throws<ArgumentException>(() => item.SetProperty(propertyName!, new object()));
    }

    [Fact]
    public void SetProperty_ThrowsArgumentNullException_WhenPropertyValueIsNull()
    {
        var item = new GameItem();

        Assert.Throws<ArgumentNullException>(() => item.SetProperty("Position", null!));
    }
}
