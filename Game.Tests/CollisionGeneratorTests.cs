using Xunit;
using Game;

namespace Game.Tests.Collision
{
    public class CollisionMatrixGeneratorTests
    {
        private readonly CollisionMatrixGenerator _generator;

        public CollisionMatrixGeneratorTests()
        {
            _generator = new CollisionMatrixGenerator();
        }

        [Fact]
        public void Generate_ShouldReturnCorrectHeaderAsFirstElement()
        {
            var result = _generator.Generate(0, 0, 0, 0, 1.0);

            Assert.Equal("dx,dy,ddx,ddy", result[0]);
        }

        [Fact]
        public void IsColliding_StationaryObjects_InsideRadius_ShouldCollide()
        {
            int minPos = 0, maxPos = 0;
            int minVel = 0, maxVel = 0;
            double radius = 1.0;

            List<string> result = _generator.Generate(minPos, maxPos, minVel, maxVel, radius);

            Assert.Contains("0,0,0,0", result);
        }

        [Fact]
        public void IsColliding_StationaryObjects_OutsideRadius_ShouldNotCollide()
        {
            int minPos = 0, maxPos = 2;
            int minVel = 0, maxVel = 0;
            double radius = 1.0;

            List<string> result = _generator.Generate(minPos, maxPos, minVel, maxVel, radius);

            Assert.DoesNotContain("0,2,0,0", result);
        }

        [Fact]
        public void IsColliding_MovingAway_StartsInsideRadius_ShouldCollide()
        {
            int minPos = 0, maxPos = 0;
            int minVel = 0, maxVel = 1;
            double radius = 1.0;

            List<string> result = _generator.Generate(minPos, maxPos, minVel, maxVel, radius);

            Assert.Contains("0,0,0,1", result);
        }

        [Fact]
        public void IsColliding_MovingAway_StartsOutsideRadius_ShouldNotCollide()
        {
            int minPos = 0, maxPos = 2;
            int minVel = 0, maxVel = 1;
            double radius = 1.0;

            List<string> result = _generator.Generate(minPos, maxPos, minVel, maxVel, radius);

            Assert.DoesNotContain("0,2,0,1", result);
        }

        [Fact]
        public void IsColliding_Approaching_FinishesInsideRadius_ShouldCollide()
        {
            int minPos = -2, maxPos = 0;
            int minVel = 0, maxVel = 1;
            double radius = 2.0;

            List<string> result = _generator.Generate(minPos, maxPos, minVel, maxVel, radius);

            Assert.Contains("-2,0,0,1", result);
        }

        [Fact]
        public void IsColliding_Approaching_FinishesOutsideRadius_ShouldNotCollide()
        {
            int minPos = -2, maxPos = 0;
            int minVel = 0, maxVel = 1;
            double radius = 0.5;

            List<string> result = _generator.Generate(minPos, maxPos, minVel, maxVel, radius);

            Assert.DoesNotContain("-2,0,0,1", result);
        }

        [Fact]
        public void IsColliding_CrossingInsideSegment_ShouldCollide()
        {
            int minPos = -1, maxPos = 0;
            int minVel = 0, maxVel = 2;
            double radius = 1.0;

            List<string> result = _generator.Generate(minPos, maxPos, minVel, maxVel, radius);

            Assert.Contains("-1,0,0,2", result);
        }

        [Fact]
        public void IsColliding_FlyingPastInsideSegment_ShouldNotCollide()
        {
            int minPos = -1, maxPos = 2;
            int minVel = 0, maxVel = 2;
            double radius = 1.0;

            List<string> result = _generator.Generate(minPos, maxPos, minVel, maxVel, radius);

            Assert.DoesNotContain("-1,2,0,2", result);
        }
    }
}
