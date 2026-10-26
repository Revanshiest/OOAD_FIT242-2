namespace Game;

public interface ICommandInjectable
{
    void Inject(ICommand command);
}
