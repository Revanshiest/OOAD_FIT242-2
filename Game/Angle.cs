public class Angle
{
    private static int denominator = 8;

    public int Numerator { get; private set; }

    public Angle(int numerator)
    {
        Numerator = numerator;
    }

    public static Angle operator +(Angle a, Angle b)
    {
        return new Angle((a.Numerator + b.Numerator) % denominator);
    }

    public static double Cos(Angle angle)
    {
        return Math.Cos(angle);
    }

    public static double Sin(Angle angle)
    {
        return Math.Sin(angle);
    }

    public static bool operator ==(Angle? a, Angle? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Equals(b);
    }

    public static bool operator !=(Angle? a, Angle? b)
    {
        return !(a==b);
    }

    public bool Equals(Angle? other)
    {
        if (other is null) return false;
        return (Numerator % denominator) == (other.Numerator % denominator);
    }

    public override bool Equals(object? obj)
    {
        return obj is Angle angle && Equals(angle);
    }

    public override int GetHashCode()
    {
        return (Numerator % denominator).GetHashCode();
    }

    private double ToRadians()
    {
        return (Numerator * Math.PI * 2) / denominator;
    }

    public static implicit operator double(Angle angle)
    {
        return angle.ToRadians();
    }
}
