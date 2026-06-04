namespace Game;

public class MoveCommand(IMovingObject movingObject) : ICommand
{
    public void Execute()
    {
        movingObject.Position += movingObject.Speed;
    }
}
