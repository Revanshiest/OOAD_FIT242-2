namespace Game.Commands;

public sealed class MoveCommand(IMovingObject movingObject) : ICommand
{
    public void Execute()
    {
        movingObject.Position += movingObject.Speed;
    }
}
