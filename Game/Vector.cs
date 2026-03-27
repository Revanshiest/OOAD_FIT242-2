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
        if (ReferenceEquals(a, b)) return true;
        if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return false;
        return a.Coordinates.SequenceEqual(b.Coordinates);
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
