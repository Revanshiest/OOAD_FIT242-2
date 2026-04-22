public class AngleTest
{
    [Fact]
    public void TestAdditionProducesCorrectAngle()
    {
        var a = new Angle(5);
        var b = new Angle(7);
        var result = a + b;
        Assert.Equal(new Angle(4), result);
    }

    [Fact]
    public void TestEqualsForEqualAngles()
    {
        var a = new Angle(15);
        var b = new Angle(23);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void TestEqualityOperatorForEqualAngles()
    {
        var a = new Angle(15);
        var b = new Angle(23);
        Assert.True(a == b);
    }

    [Fact]
    public void TestEqualityOperatorBothNull()
    {
        Angle? a = null;
        Angle? b = null;
        Assert.True(a == b);
    }

    [Fact]
    public void TestEqualityOperatorLeftNull()
    {
        Angle? a = null;
        var b = new Angle(5);
        Assert.False(a == b);
    }

    [Fact]
    public void TestEqualityOperatorRightNull()
    {
        var a = new Angle(5);
        Angle? b = null;
        Assert.False(a == b);
    }

    [Fact]
    public void TestInequalityOperatorBothNull()
    {
        Angle? a = null;
        Angle? b = null;
        Assert.False(a != b);
    }

    [Fact]
    public void TestEqualsForUnequalAngles()
    {
        var a = new Angle(1);
        var b = new Angle(2);
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void TestInequalityOperatorForUnequalAngles()
    {
        var a = new Angle(1);
        var b = new Angle(2);
        Assert.True(a != b);
    }

    [Fact]
    public void TestHashCodePresence()
    {
        var angle = new Angle(15);
        var hashCode = angle.GetHashCode();
        Assert.Equal(7, hashCode);
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenNull()
    {
        var angle = new Angle(3);
        Assert.False(angle.Equals(null));
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenDifferentType()
    {
        var angle = new Angle(3);
        Assert.False(angle.Equals("Not Angle"));
    }

    [Fact]
    public void Equals_ReturnsTrue_WhenEqual()
    {
        var angle1 = new Angle(10);
        var angle2 = new Angle(2);

        Assert.True(angle1.Equals(angle2));
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenDifferent()
    {
        var angle1 = new Angle(3);
        var angle2 = new Angle(4);

        Assert.False(angle1.Equals(angle2));
    }

    [Fact]
    public void ToRadians_ReturnsZero_ForZeroAngle()
    {
        var angle = new Angle(0);
        double radians = angle;

        Assert.Equal(0, radians, precision: 5);
    }

    [Fact]
    public void ToRadians_ReturnsPiOver4_ForOneEighth()
    {
        var angle = new Angle(1);
        double radians = angle;

        Assert.Equal(Math.PI / 4, radians, precision: 5);
    }

    [Fact]
    public void ToRadians_ReturnsPi_ForHalfCircle()
    {
        var angle = new Angle(4);
        double radians = angle;

        Assert.Equal(Math.PI, radians, precision: 5);
    }
    private const double Precision = 1e-5;

    [Fact]
    public void Cos_ReturnsOne_ForZeroAngle()
    {
        var angle = new Angle(0);
        double cosValue = Angle.Cos(angle);

        Assert.Equal(1.0, cosValue, Precision);
    }

    [Fact]
    public void Sin_ReturnsOne_ForQuarterCircle()
    {
        var angle = new Angle(2);
        double sinValue = Angle.Sin(angle);

        Assert.Equal(1.0, sinValue, Precision);
    }
}
