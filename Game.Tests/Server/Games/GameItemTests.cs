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

    [Fact]
    public void ReadOnlyGameItem_ProvidesImmutableContract_AndLiskovSubstitution()
    {
        var properties = new Dictionary<string, object>
        {
            ["Position"] = new Vector([100, 200]),
            ["Type"] = "Asteroid"
        };

        IReadOnlyGameItem readOnlyItem = new ReadOnlyGameItem(properties);

        Assert.Equal(2, readOnlyItem.PropertyNames.Count);
        Assert.Equal(new Vector([100, 200]), readOnlyItem.GetProperty("Position"));
        Assert.Equal("Asteroid", readOnlyItem.GetProperty("Type"));

        var mutableItem = new GameItem();
        mutableItem.SetProperty("Velocity", new Vector([1, 0]));

        Assert.IsAssignableFrom<IReadOnlyGameItem>(mutableItem);
        Assert.IsAssignableFrom<IMutableGameItem>(mutableItem);
        Assert.IsAssignableFrom<IGameItem>(mutableItem);

        IGameItem readOnlyAsGameItem = new ReadOnlyGameItem(properties);
        var scope = new Scope();
        scope.Register("obs1", readOnlyAsGameItem);
        Assert.True(scope.Contains("obs1"));
        Assert.Equal(new Vector([100, 200]), scope.GetItem("obs1").GetProperty("Position"));
        Assert.Throws<NotSupportedException>(() => scope.GetItem("obs1").SetProperty("Position", new Vector([0, 0])));
    }
}
