namespace Game;

public interface IMessageReceiver
{
    public void Receive(ICommand cmd);
    bool CanAccept(ICommand command);
}
