using Game.IoC;
using GameServer.Games;
using GameServer.Games.Pipeline;

namespace GameServer.IoC;

public sealed class RegisterIoCDependencyGameItemPipeline : ICommand
{
    public const string StrategyKey = "Games.CreateItem";

    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", StrategyKey, (object[] args) =>
        {
            var scope = StrategyArguments.Get<IScope>(args, 0);
            var specification = StrategyArguments.Get<GameItemCreationSpecification>(args, 1);

            return new CreateGameItemPipeline(scope, specification).Build();
        }).Execute();
    }
}
