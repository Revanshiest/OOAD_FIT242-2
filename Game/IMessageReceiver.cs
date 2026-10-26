namespace Game;

public interface IMessageReceiver
{
    void Receive(ICommand command);
    bool CanAccept(ICommand command);
}
