namespace GameServer.IoC;

using global::Game;
using global::Game.Commands;
using GameServer.Games;
using GameServer.Games.Pipeline;

public sealed class RegisterIoCDependencyServerItemAdapters : ICommand
{
    public const string AdapterDependencyKey = "Adapters.IMovingObject";

    private static readonly (Type TargetType, Func<object, IMovingObject> Factory)[] AdapterRules =
    [
        (typeof(IMovingObject), targetObject => (IMovingObject)targetObject),
        (typeof(IGameItem), targetObject => new GameItemMovingObjectAdapter((IGameItem)targetObject))
    ];

    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", AdapterDependencyKey, (object[] arguments) =>
        {
            var target = arguments[0];
            var rule = AdapterRules.FirstOrDefault(adapterRule => adapterRule.TargetType.IsInstanceOfType(target));

            return rule.Factory?.Invoke(target)
                ?? throw new ArgumentException($"Cannot adapt object of type '{target.GetType()}' to IMovingObject.");
        }).Execute();
    }
}
