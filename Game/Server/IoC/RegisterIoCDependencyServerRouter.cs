namespace GameServer.IoC;

using Game.IoC;
using GameServer.Messaging;
using GameServer.Routing;
using GameServer.Resharding;

public sealed class RegisterIoCDependencyServerRouter : ICommand
{
    public const string CreateRouterKey = "Server.Router.Create";

    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", CreateRouterKey, (object[] args) =>
        {
            var writersByThreadId = StrategyArguments.Get<IReadOnlyList<IMessageQueueWriter<InboundMessage>>>(args, 0);
            var threadCount = StrategyArguments.Get<int>(args, 1);

            return new PrimaryRouter(writersByThreadId, new ReshardingTable(threadCount));
        }).Execute();
    }
}
