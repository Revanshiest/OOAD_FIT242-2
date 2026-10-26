namespace Game;

public sealed class Vector : IEquatable<Vector>
{
    private readonly int[] coordinates;

    public Vector(int[] coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);

        this.coordinates = [.. coordinates];
    }

    public static Vector operator +(Vector left, Vector right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return left.coordinates.Length == right.coordinates.Length
            ? new Vector(left.coordinates.Zip(right.coordinates, (leftCoordinate, rightCoordinate) => leftCoordinate + rightCoordinate).ToArray())
            : throw new ArgumentException("Vectors must be of the same length.");
    }

    public static bool operator ==(Vector? left, Vector? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Vector? left, Vector? right) => !(left == right);

    public bool Equals(Vector? other) =>
        other is not null && coordinates.SequenceEqual(other.coordinates);

    public override bool Equals(object? obj) => obj is Vector other && Equals(other);

    public override int GetHashCode() =>
        coordinates.Aggregate(17, (current, coordinate) => current * 31 + coordinate);
}
