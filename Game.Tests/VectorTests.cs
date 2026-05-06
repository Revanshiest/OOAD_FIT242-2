using Xunit;
using System;

namespace Game.Tests;

public class VectorTest
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
    public void GetHashCode_ReturnsUniqueHashCode()
    {
        Vector v1 = new Vector(new int[] { 1, 2 });
        int hashCode = v1.GetHashCode();
        Assert.NotEqual(0, hashCode);
    }

    [Fact]
    public void EqualsOperator_BothNull_ReturnsTrue()
    {
        Vector v1 = null;
        Vector v2 = null;

        Assert.True(v1 == v2);
    }

    [Fact]
    public void EqualsOperator_OneNull_ReturnsFalse()
    {
        Vector v1 = new Vector(new int[] { 1, 2 });
        Vector v2 = null;

        Assert.False(v1 == v2);
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
