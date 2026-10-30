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

        var repository = Ioc.Resolve<IGameObjectRepository>("Repository.GameObject");

        Assert.NotNull(repository);
        Assert.IsType<GameObjectRepository>(repository);
    }

    [Fact]
    public void Execute_ResolvesSameRepositoryInstance_OnEveryResolution()
    {
        new RegisterIoCDependencyGameObjectRepository().Execute();

        var firstResolution = Ioc.Resolve<IGameObjectRepository>("Repository.GameObject");
        var secondResolution = Ioc.Resolve<IGameObjectRepository>("Repository.GameObject");

        Assert.Same(firstResolution, secondResolution);
    }
}
