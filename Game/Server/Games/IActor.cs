namespace GameServer.Games;

public interface IActor
{
    string ActorId { get; }

    void Tell(ICommand command);

    void SendMessage(IActor targetActor, ICommand command);

    IActor CreateChildActor(string childActorId, GamePriority priority);
}

