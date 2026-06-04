using System;
using System.Collections.Generic;
using Xunit;
using Game;

namespace Game.Tests;

public class MovingObjectAdapterTests
{
    [Fact]
    public void Position_Get_ReturnsValueFromDictionary()
    {
        var expected = new Vector(new int[] { 10, 20 });
        var gameObject = new Dictionary<string, object>
        {
            { "Position", expected },
            { "Speed", new Vector(new int[] { 1, 1 }) }
        };

        var adapter = new MovingObjectAdapter(gameObject);

        Assert.Equal(expected, adapter.Position);
    }

    [Fact]
    public void Position_Set_UpdatesDictionary()
    {
        var gameObject = new Dictionary<string, object>
        {
            { "Position", new Vector(new int[] { 0, 0 }) },
            { "Speed", new Vector(new int[] { 1, 1 }) }
        };

        var adapter = new MovingObjectAdapter(gameObject);
        var newPosition = new Vector(new int[] { 5, 10 });

        adapter.Position = newPosition;

        Assert.Equal(newPosition, (Vector)gameObject["Position"]);
    }

    [Fact]
    public void Speed_Get_ReturnsValueFromDictionary()
    {
        var expected = new Vector(new int[] { 3, 4 });
        var gameObject = new Dictionary<string, object>
        {
            { "Position", new Vector(new int[] { 0, 0 }) },
            { "Speed", expected }
        };

        var adapter = new MovingObjectAdapter(gameObject);

        Assert.Equal(expected, adapter.Speed);
    }

    [Fact]
    public void MoveCommand_MovesTorpedo_ThroughAdapter()
    {
        var gameObject = new Dictionary<string, object>
        {
            { "Position", new Vector(new int[] { 10, 20 }) },
            { "Speed", new Vector(new int[] { 1, 0 }) }
        };

        var adapter = new MovingObjectAdapter(gameObject);
        var moveCommand = new MoveCommand(adapter);

        moveCommand.Execute();

        Assert.Equal(new Vector(new int[] { 11, 20 }), (Vector)gameObject["Position"]);
    }

    [Fact]
    public void Constructor_ThrowsWhenGameObjectIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MovingObjectAdapter(null!));
    }
}
