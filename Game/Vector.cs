namespace Game;

public class Vector
{
    public int[] Coordinates { get; }

    public Vector(int[] Coordinates)
    {
        this.Coordinates = Coordinates;
    }

    public static Vector operator +(Vector a, Vector b) =>

    a.Coordinates.Length == b.Coordinates.Length
        ? new Vector(a.Coordinates.Zip(b.Coordinates, (x, y) => x + y).ToArray())
        : throw new ArgumentException("Vectors must be of the same length.");

    public static bool operator ==(Vector a, Vector b)
    {
        return a.Coordinates.SequenceEqual(b.Coordinates);
    }

    public static bool operator !=(Vector a, Vector b)
    {
        return !(a == b);
    }

    public override bool Equals(object? obj)
    {
        if (obj is Vector other)
        {
            return Coordinates.SequenceEqual(other.Coordinates);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return Coordinates.Aggregate(17, (current, item) => current * 31 + item);
    }
}
