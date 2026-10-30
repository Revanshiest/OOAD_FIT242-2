namespace Game.IoC;

public sealed class RegisterIoCDependencyGameObjectRepository : ICommand
{
    public void Execute()
    {
        var gameObjectRepository = new GameObjectRepository();

        Ioc.Resolve<ICommand>("IoC.Register", "Repository.GameObject",
            (object[] args) => gameObjectRepository).Execute();
    }
}
