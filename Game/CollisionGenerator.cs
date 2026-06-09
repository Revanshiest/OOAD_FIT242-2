public class CollisionMatrixGenerator
{
    public List<string> Generate(int minPos, int maxPos, int minVel, int maxVel, double radius)
    {
        double R2 = radius * radius;
        var collisions = new List<string> { "dx,dy,ddx,ddy" };

        for (int ddx = minVel; ddx <= maxVel; ddx++)
            for (int ddy = minVel; ddy <= maxVel; ddy++)
                for (int dx = minPos; dx <= maxPos; dx++)
                    for (int dy = minPos; dy <= maxPos; dy++)
                    {
                        if (IsColliding(dx, dy, ddx, ddy, R2))
                        {
                            collisions.Add($"{dx},{dy},{ddx},{ddy}");
                        }
                    }

        return collisions;
    }

    private bool IsColliding(int dx, int dy, int ddx, int ddy, double R2)
    {
        double A = ddx * ddx + ddy * ddy;
        double C = dx * dx + dy * dy;

        if (A == 0)
        {
            return C <= R2;
        }

        double B = 2.0 * (dx * ddx + dy * ddy);
        double t_min = -B / (2.0 * A);
        double D2;

        if (t_min <= 0)
        {
            D2 = C;
        }
        else if (t_min >= 1)
        {
            D2 = A + B + C;
        }
        else
        {
            D2 = C - (B * B) / (4.0 * A);
        }

        return D2 <= R2;
    }
}
