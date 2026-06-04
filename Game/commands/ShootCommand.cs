using System;
using System.Collections.Generic;

namespace Game;

public class ShootCommand : ICommand
{
    private readonly IDictionary<string, object> _ship;

    public ShootCommand(IDictionary<string, object> ship)
    {
        _ship = ship ?? throw new ArgumentNullException(nameof(ship));
    }

    public void Execute()
    {
        var position = (Vector)_ship["Position"];
        var angle = (Angle)_ship["Angle"];

        var torpedoSpeed = new Vector(new int[]
        {
            (int)Math.Round(Angle.Cos(angle)),
            (int)Math.Round(Angle.Sin(angle))
        });

        var torpedo = new Dictionary<string, object>
        {
            { "Position", new Vector(position.Coordinates.ToArray()) },
            { "Speed", torpedoSpeed }
        };

        var torpedoId = Guid.NewGuid().ToString();

        var repository = Ioc.Resolve<IGameObjectRepository>("Repository.GameObject");
        repository.Add(torpedoId, torpedo);

        Ioc.Resolve<ICommand>("Actions.Start", torpedo, "Move").Execute();
    }
}
