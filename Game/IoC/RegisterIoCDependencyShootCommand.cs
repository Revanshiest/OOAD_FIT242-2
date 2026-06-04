namespace Game;

public class RegisterIoCDependencyShootCommand : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Commands.Shoot",
            (object[] args) =>
            {
                var ship = (IDictionary<string, object>)args[0];
                return new ShootCommand(ship);
            }
        ).Execute();
    }
}
