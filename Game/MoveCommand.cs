namespace Game;

using App;
public class MoveCommand(IMovingObject movingObject) : ICommand
{
    public void Execute()
    {
        movingObject.Position += movingObject.Speed;
    }
}
