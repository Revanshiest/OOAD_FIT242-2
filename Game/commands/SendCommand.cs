namespace Game;

public class SendCommand : ICommand
{
    private readonly ICommand _command;
    private readonly IMessageReceiver _receiver;

    public SendCommand(ICommand command, IMessageReceiver receiver)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
    }

    public void Execute()
    {
        if (!_receiver.CanAccept(_command))
        {
            throw new InvalidOperationException("recipient is busy");
        }

        _receiver.Receive(_command);
    }
}
