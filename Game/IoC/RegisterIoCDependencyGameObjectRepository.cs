namespace Game.IoC;

public sealed class RegisterIoCDependencyGameObjectRepository : ICommand
{
    public void Execute()
    {
        var gameObjectRepository = new GameObjectRepository();

        Ioc.Resolve<ICommand>(IocDependencyKeys.Register, IocDependencyKeys.GameObjectRepository,
            (object[] args) => gameObjectRepository).Execute();
    }
}
