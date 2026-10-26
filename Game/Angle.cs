namespace Game;

public sealed class Angle : IEquatable<Angle>
{
    private const int FullCircleEighths = 8;

    public int Numerator { get; }

    public Angle(int numerator)
    {
        Numerator = numerator;
    }

    public static Angle operator +(Angle left, Angle right) =>
        new((left.Numerator + right.Numerator) % FullCircleEighths);

    public static double Cos(Angle angle) => Math.Cos(angle.ToRadians());

    public static double Sin(Angle angle) => Math.Sin(angle.ToRadians());

    public static bool operator ==(Angle? left, Angle? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Angle? left, Angle? right) => !(left == right);

    public bool Equals(Angle? other) =>
        other is not null && (Numerator % FullCircleEighths) == (other.Numerator % FullCircleEighths);

    public override bool Equals(object? obj) => obj is Angle other && Equals(other);

    public override int GetHashCode() => (Numerator % FullCircleEighths).GetHashCode();

    private double ToRadians() => Numerator * 2.0 * Math.PI / FullCircleEighths;

    public static implicit operator double(Angle angle) => angle.ToRadians();
}
