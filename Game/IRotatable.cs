namespace Game;

public interface IRotatable
{
    Angle Angle { get; set; }
    Angle AngleVelocity { get; }
}
