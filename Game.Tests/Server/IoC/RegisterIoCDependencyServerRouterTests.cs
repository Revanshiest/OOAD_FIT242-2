using GameServer.IoC;
using GameServer.Messaging;
using GameServer.Routing;
using Game.Tests.Server.Messaging;

namespace Game.Tests.Server.IoC;

public class RegisterIoCDependencyServerRouterTests
{
    [Fact]
    public void Resolve_CreatesPrimaryRouter_ThatDispatchesThroughGeneratedTable()
    {
        new App.Scopes.InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();
        new RegisterIoCDependencyServerRouter().Execute();

        var targetWriters = new List<IMessageQueueWriter<InboundMessage>> { new RecordingMessageWriter(), new RecordingMessageWriter() };
        var resolvedRouter = Ioc.Resolve<PrimaryRouter>(RegisterIoCDependencyServerRouter.CreateRouterKey, targetWriters, 2);

        var routeTarget = resolvedRouter.ResolveTarget(7, "game-1");

        Assert.Same(targetWriters[1], routeTarget.Writer);
        Assert.Equal(1, routeTarget.ThreadId);
    }

    [Fact]
    public void Execute_RegistersReplaceableStrategy()
    {
        new App.Scopes.InitCommand().Execute();
        var firstScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", firstScope).Execute();
        new RegisterIoCDependencyServerRouter().Execute();

        var firstScopeRouter = Ioc.Resolve<PrimaryRouter>(
            RegisterIoCDependencyServerRouter.CreateRouterKey,
            new List<IMessageQueueWriter<InboundMessage>> { new RecordingMessageWriter() },
            1);

        var secondScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", secondScope).Execute();
        new RegisterIoCDependencyServerRouter().Execute();

        var secondScopeRouter = Ioc.Resolve<PrimaryRouter>(
            RegisterIoCDependencyServerRouter.CreateRouterKey,
            new List<IMessageQueueWriter<InboundMessage>> { new RecordingMessageWriter() },
            1);

        Assert.NotNull(firstScopeRouter);
        Assert.NotNull(secondScopeRouter);
        Assert.NotSame(firstScopeRouter, secondScopeRouter);
    }
}
