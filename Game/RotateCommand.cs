using App;
public class RotateCommand(IRotatable rotatableObject) : ICommand
{
    public void Execute()
    {
        rotatableObject.Angle += rotatableObject.AngleVelocity;
    }
}

