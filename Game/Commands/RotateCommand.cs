namespace Game.Commands;

public sealed class RotateCommand(IRotatable rotatableObject) : ICommand
{
    public void Execute()
    {
        rotatableObject.Angle += rotatableObject.AngleVelocity;
    }
}
