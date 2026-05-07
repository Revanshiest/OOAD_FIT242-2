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
        if (a.Coordinates.Length != b.Coordinates.Length)
        {
            throw new ArgumentException("Vectors must be of the same length.");
        }

        int[] ResultCoordinates = new int[a.Coordinates.Length];
        for (int i = 0; i < a.Coordinates.Length; i++)
        {
            ResultCoordinates[i] = a.Coordinates[i] + b.Coordinates[i];
        }

        return new Vector(ResultCoordinates);
    }

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
