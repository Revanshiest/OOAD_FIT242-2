namespace Game;

public class Vector
{
    public int[] Coordinates { get; }

    public Vector(int[] Coordinates)
    {
        this.Coordinates = Coordinates;
    }

    public static Vector operator +(Vector a, Vector b)
    {
        throw new NotImplementedException();
    }

    public static bool operator ==(Vector a, Vector b)
    {
        throw new NotImplementedException();
    }

    public static bool operator !=(Vector a, Vector b)
    {
        throw new NotImplementedException();
    }

    public override bool Equals(object? obj)
    {
        throw new NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}
