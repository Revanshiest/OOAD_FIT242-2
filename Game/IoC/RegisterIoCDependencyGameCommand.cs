namespace Game;

public class RegisterIoCDependencyGameCommand : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Game.Command",
            (object[] args) =>
            {
                var order = (IDictionary<string, object>)args[0];
                return new GameCommand(order);
            }
        ).Execute();
    }
}
