using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class CollisionMatrixGenerator
{
    public List<string> Generate(int minPos, int maxPos, int minVel, int maxVel, double radius)
    {
        double R2 = radius * radius;

        var collisions = Enumerable.Range(minVel, maxVel - minVel + 1)
            .SelectMany(ddx => Enumerable.Range(minVel, maxVel - minVel + 1)
                .SelectMany(ddy => Enumerable.Range(minPos, maxPos - minPos + 1)
                    .SelectMany(dx => Enumerable.Range(minPos, maxPos - minPos + 1)
                        .Where(dy => IsColliding(dx, dy, ddx, ddy, R2))
                        .Select(dy => $"{dx},{dy},{ddx},{ddy}"))))
            .ToList();

        collisions.Insert(0, "dx,dy,ddx,ddy");

        return collisions;
    }

    public void SaveToFile(string filePath, List<string> collisions)
    {
        File.WriteAllLines(filePath, collisions);
    }

    public HashSet<(int dx, int dy, int ddx, int ddy)> LoadFromFile(string filePath)
    {
        return File.ReadAllLines(filePath)
            .Skip(1)
            .Select(line => line.Split(','))
            .Select(parts => (
                int.Parse(parts[0]),
                int.Parse(parts[1]),
                int.Parse(parts[2]),
                int.Parse(parts[3])))
            .ToHashSet();
    }

    private bool IsColliding(int dx, int dy, int ddx, int ddy, double R2)
    {
        double A = ddx * ddx + ddy * ddy;
        double C = dx * dx + dy * dy;
        double B = 2.0 * (dx * ddx + dy * ddy);
        double t_min = A == 0 ? 0.0 : -B / (2.0 * A);

        double D2 = A == 0
            ? C
            : t_min <= 0
                ? C
                : t_min >= 1
                    ? A + B + C
                    : C - (B * B) / (4.0 * A);

        return D2 <= R2;
    }
}
