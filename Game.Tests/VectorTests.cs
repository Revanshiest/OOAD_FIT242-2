using Game;

namespace Game.Tests;

public class VectorTests
{
    [Fact]
    public void Add_Vectors_CorrectSum()
    {
        Vector v1 = new Vector(new int[] { 1, -1, 2 });
        Vector v2 = new Vector(new int[] { -1, 1, -2 });
        Vector expected = new Vector(new int[] { 0, 0, 0 });

        Vector result = v1 + v2;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Add_Vectors_DifferentLength_ThrowsArgumentException()
    {
        Vector v1 = new Vector(new int[] { 1, 2, 3 });
        Vector v2 = new Vector(new int[] { 1, 2 });

        var exception = Assert.Throws<ArgumentException>(() => { Vector result = v1 + v2; });
        Assert.Equal("Vectors must be of the same length.", exception.Message);
    }

    [Fact]
    public void Add_EmptyVectors_ReturnsEmptyVector()
    {
        var v1 = new Vector(new int[] { });
        var v2 = new Vector(new int[] { });

        var result = v1 + v2;

        Assert.Equal(new Vector(new int[] { }), result);
    }

    [Fact]
    public void Add_NullLeftOperand_ThrowsArgumentNullException()
    {
        var vector = new Vector(new int[] { 1, 2 });
        Vector? nullVector = null;

        Assert.Throws<ArgumentNullException>(() => { var result = nullVector! + vector; });
    }

    [Fact]
    public void Add_NullRightOperand_ThrowsArgumentNullException()
    {
        var vector = new Vector(new int[] { 1, 2 });
        Vector? nullVector = null;

        Assert.Throws<ArgumentNullException>(() => { var result = vector + nullVector!; });
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenCoordinatesAreNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Vector(null!));
    }

    [Fact]
    public void Constructor_CopiesCoordinates_DefensiveCopy()
    {
        var coordinates = new int[] { 1, 2 };
        var vector = new Vector(coordinates);

        coordinates[0] = 100;

        Assert.Equal(new Vector(new int[] { 1, 2 }), vector);
    }

    [Fact]
    public void Add_Vectors_DifferentLength_ThrowsArgumentException2()
    {
        Vector v1 = new Vector(new int[] { 1, 2 });
        Vector v2 = new Vector(new int[] { 1, 2, 3 });

        var exception = Assert.Throws<ArgumentException>(() => { Vector result = v1 + v2; });
        Assert.Equal("Vectors must be of the same length.", exception.Message);
    }

    [Fact]
    public void Equals_SameCoordinates_DifferentObjects_ReturnsTrue()
    {
        Vector v1 = new Vector(new int[] { 1, 2 });
        Vector v2 = new Vector(new int[] { 1, 2 });

        Assert.True(v1.Equals(v2));
    }

    [Fact]
    public void EqualsOperator_SameCoordinates_DifferentObjects_ReturnsTrue()
    {
        Vector v1 = new Vector(new int[] { 1, 2 });
        Vector v2 = new Vector(new int[] { 1, 2 });

        Assert.True(v1 == v2);
    }

    [Fact]
    public void Equals_SameLengthDifferentCoordinates_ReturnsFalse()
    {
        Vector v1 = new Vector(new int[] { 1, 2 });
        Vector v2 = new Vector(new int[] { 2, 3 });

        Assert.False(v1.Equals(v2));
    }

    [Fact]
    public void NotEqualsOperator_DifferentCoordinates_ReturnsTrue()
    {
        Vector v1 = new Vector(new int[] { 1, 2 });
        Vector v2 = new Vector(new int[] { 2, 3 });

        Assert.True(v1 != v2);
    }

    [Fact]
    public void GetHashCode_ReturnsNonZero()
    {
        Vector v1 = new Vector(new int[] { 1, 2 });
        int hashCode = v1.GetHashCode();
        Assert.NotEqual(0, hashCode);
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        Vector v1 = new Vector(new int[] { 1, 2 });

        Assert.False(v1.Equals(null));
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        Vector v1 = new Vector(new int[] { 1, 2 });

        Assert.False(v1.Equals("not a vector"));
    }
}
