using Xunit;
using Game;

namespace Game.Tests;

public class GameObjectRepositoryTests
{
    [Fact]
    public void Add_And_Get_ReturnsCorrectObject()
    {
        var repository = new GameObjectRepository();
        var gameObject = new Dictionary<string, object>
        {
            { "Position", new Vector(new int[] { 1, 2 }) },
            { "Speed", new Vector(new int[] { 3, 4 }) }
        };

        repository.Add("ship1", gameObject);
        var result = repository.Get("ship1");

        Assert.Same(gameObject, result);
    }

    [Fact]
    public void Get_NonExistentId_ThrowsKeyNotFoundException()
    {
        var repository = new GameObjectRepository();

        Assert.Throws<KeyNotFoundException>(() => repository.Get("nonexistent"));
    }

    [Fact]
    public void Add_DuplicateId_ThrowsInvalidOperationException()
    {
        var repository = new GameObjectRepository();
        var gameObject = new Dictionary<string, object>();

        repository.Add("ship1", gameObject);

        Assert.Throws<InvalidOperationException>(() => repository.Add("ship1", gameObject));
    }

    [Fact]
    public void Add_MultipleObjects_GetReturnsCorrectOnes()
    {
        var repository = new GameObjectRepository();
        var obj1 = new Dictionary<string, object> { { "Type", "Ship" } };
        var obj2 = new Dictionary<string, object> { { "Type", "Torpedo" } };

        repository.Add("ship1", obj1);
        repository.Add("torpedo1", obj2);

        Assert.Same(obj1, repository.Get("ship1"));
        Assert.Same(obj2, repository.Get("torpedo1"));
    }
}
