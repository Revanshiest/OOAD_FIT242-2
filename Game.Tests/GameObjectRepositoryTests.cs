using Game;

namespace Game.Tests;

public class GameObjectRepositoryTests
{
    [Fact]
    public void Add_And_Get_ReturnsSameValuesWithoutSharingMutableState()
    {
        var repository = new GameObjectRepository();
        var gameObject = new Dictionary<string, object>
        {
            { "Position", new Vector(new[] { 1, 2 }) },
            { "Speed", new Vector(new[] { 3, 4 }) }
        };

        repository.Add("ship1", gameObject);
        var retrievedGameObject = repository.Get("ship1");

        Assert.Equal(gameObject, retrievedGameObject);
        Assert.NotSame(gameObject, retrievedGameObject);
    }

    [Fact]
    public void Get_ThrowsKeyNotFoundException_WhenIdentifierNotRegistered()
    {
        var repository = new GameObjectRepository();

        Assert.Throws<KeyNotFoundException>(() => repository.Get("nonexistent"));
    }

    [Fact]
    public void Add_ThrowsInvalidOperationException_WhenIdentifierAlreadyRegistered()
    {
        var repository = new GameObjectRepository();
        var gameObject = new Dictionary<string, object>();

        repository.Add("ship1", gameObject);

        Assert.Throws<InvalidOperationException>(() => repository.Add("ship1", gameObject));
    }

    [Fact]
    public void Get_ReturnsMatchingGameObject_ForEachRegisteredIdentifier()
    {
        var repository = new GameObjectRepository();
        var shipGameObject = new Dictionary<string, object> { { "Type", "Ship" } };
        var torpedoGameObject = new Dictionary<string, object> { { "Type", "Torpedo" } };

        repository.Add("ship1", shipGameObject);
        repository.Add("torpedo1", torpedoGameObject);

        Assert.Equal(shipGameObject, repository.Get("ship1"));
        Assert.Equal(torpedoGameObject, repository.Get("torpedo1"));
    }

    [Fact]
    public void Get_ThrowsArgumentException_WhenIdentifierIsNull()
    {
        var repository = new GameObjectRepository();

        Assert.Throws<ArgumentException>(() => repository.Get(null!));
    }

    [Fact]
    public void Get_ThrowsArgumentException_WhenIdentifierIsWhitespace()
    {
        var repository = new GameObjectRepository();

        Assert.Throws<ArgumentException>(() => repository.Get("   "));
    }

    [Fact]
    public void Add_CopiesInputDictionary_ToProtectRepositoryState()
    {
        var repository = new GameObjectRepository();
        var gameObject = new Dictionary<string, object>
        {
            { "Position", new Vector(new[] { 1, 2 }) }
        };

        repository.Add("ship1", gameObject);
        gameObject["Position"] = new Vector(new[] { 9, 9 });

        Assert.Equal(new Vector(new[] { 1, 2 }), repository.Get("ship1")["Position"]);
    }

    [Fact]
    public void Add_ThrowsArgumentException_WhenIdentifierIsEmpty()
    {
        var repository = new GameObjectRepository();
        var gameObject = new Dictionary<string, object>();

        Assert.Throws<ArgumentException>(() => repository.Add(string.Empty, gameObject));
    }

    [Fact]
    public void Add_ThrowsArgumentNullException_WhenGameObjectIsNull()
    {
        var repository = new GameObjectRepository();

        Assert.Throws<ArgumentNullException>(() => repository.Add("ship1", null!));
    }
}
