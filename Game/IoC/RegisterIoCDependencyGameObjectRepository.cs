namespace Game;

public class RegisterIoCDependencyGameObjectRepository : ICommand
{
    public void Execute()
    {
        var repository = new GameObjectRepository();

        Ioc.Resolve<ICommand>("IoC.Register", "Repository.GameObject",
            (object[] args) => repository
        ).Execute();
    }
}
