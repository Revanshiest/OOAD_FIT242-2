namespace Game;

public class RegisterIoCDependencyAuthorization : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Auth.Check",
            (object[] args) =>
            {
                var playerId = (string)args[0];
                var gameObjectId = (string)args[1];

                var allowedPlayers = Ioc.Resolve<HashSet<string>>($"Auth.AllowedPlayers.{gameObjectId}");

                if (!allowedPlayers.Contains(playerId))
                {
                    throw new UnauthorizedAccessException(
                        $"Player '{playerId}' is not authorized to control object '{gameObjectId}'");
                }

                return new EmptyCommand();
            }
        ).Execute();
    }
}
