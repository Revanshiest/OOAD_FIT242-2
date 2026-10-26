using Game;
using Game.IoC;
using Xunit;

namespace Game.Tests;

public class RegisterIoCDependencyGameObjectRepositoryTests
{
    public RegisterIoCDependencyGameObjectRepositoryTests()
    {
        IoCTestScope.Initialize();
    }

    [Fact]
    public void Execute_RegistersGameObjectRepositoryDependency()
    {
        new RegisterIoCDependencyGameObjectRepository().Execute();

        var repository = Ioc.Resolve<IGameObjectRepository>(IocDependencyKeys.GameObjectRepository);

        Assert.NotNull(repository);
        Assert.IsType<GameObjectRepository>(repository);
    }

    [Fact]
    public void Execute_ResolvesSameRepositoryInstance_OnEveryResolution()
    {
        new RegisterIoCDependencyGameObjectRepository().Execute();

        var firstResolution = Ioc.Resolve<IGameObjectRepository>(IocDependencyKeys.GameObjectRepository);
        var secondResolution = Ioc.Resolve<IGameObjectRepository>(IocDependencyKeys.GameObjectRepository);

        Assert.Same(firstResolution, secondResolution);
    }
}
