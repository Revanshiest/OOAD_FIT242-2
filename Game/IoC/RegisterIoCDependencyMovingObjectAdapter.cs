namespace Game;

public class RegisterIoCDependencyMovingObjectAdapter : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Adapters.IMovingObject",
            (object[] args) =>
            {
                var gameObject = (IDictionary<string, object>)args[0];
                return new MovingObjectAdapter(gameObject);
            }
        ).Execute();
    }
}
