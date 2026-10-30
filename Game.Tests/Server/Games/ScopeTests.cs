using GameServer.Games;

namespace Game.Tests.Server.Games;

public class ScopeTests
{
    [Fact]
    public void Register_And_GetItem_ReturnSameInstance()
    {
        var scope = new Scope();
        var item = new GameItem();

        scope.Register("ship1", item);

        Assert.Same(item, scope.GetItem("ship1"));
        Assert.True(scope.Contains("ship1"));
    }

    [Fact]
    public void GetItem_ThrowsKeyNotFoundException_WhenItemNotRegistered()
    {
        var scope = new Scope();

        Assert.Throws<KeyNotFoundException>(() => scope.GetItem("missing"));
    }

    [Fact]
    public void Register_ThrowsInvalidOperationException_WhenItemAlreadyRegistered()
    {
        var scope = new Scope();
        scope.Register("ship1", new GameItem());

        Assert.Throws<InvalidOperationException>(() => scope.Register("ship1", new GameItem()));
    }

    [Fact]
    public void Unregister_RemovesItemFromScope()
    {
        var scope = new Scope();
        scope.Register("ship1", new GameItem());

        scope.Unregister("ship1");

        Assert.False(scope.Contains("ship1"));
    }

    [Fact]
    public void Unregister_ThrowsKeyNotFoundException_WhenItemNotRegistered()
    {
        var scope = new Scope();

        Assert.Throws<KeyNotFoundException>(() => scope.Unregister("missing"));
    }

    [Fact]
    public void ItemIds_ReturnsAllRegisteredIdentifiers()
    {
        var scope = new Scope();
        scope.Register("ship1", new GameItem());
        scope.Register("torpedo1", new GameItem());

        var itemIds = scope.ItemIds;

        Assert.Equal(2, itemIds.Count);
        Assert.Contains("ship1", itemIds);
        Assert.Contains("torpedo1", itemIds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetItem_ThrowsArgumentException_WhenIdentifierIsInvalid(string? itemId)
    {
        var scope = new Scope();

        Assert.Throws<ArgumentException>(() => scope.GetItem(itemId!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_ThrowsArgumentException_WhenIdentifierIsInvalid(string? itemId)
    {
        var scope = new Scope();

        Assert.Throws<ArgumentException>(() => scope.Register(itemId!, new GameItem()));
    }

    [Fact]
    public void Register_ThrowsArgumentNullException_WhenItemIsNull()
    {
        var scope = new Scope();

        Assert.Throws<ArgumentNullException>(() => scope.Register("ship1", null!));
    }
}
