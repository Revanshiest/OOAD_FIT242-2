using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Game;

namespace Game.Tests.Collision
{
    public class CollisionMatrixGeneratorTests : IDisposable
    {
        private readonly CollisionMatrixGenerator _generator;
        private readonly List<string> _tempFiles;

        public CollisionMatrixGeneratorTests()
        {
            _generator = new CollisionMatrixGenerator();
            _tempFiles = new List<string>();
        }

        public void Dispose()
        {
            _tempFiles.ForEach(f =>
            {
                try { File.Delete(f); } catch { }
            });
        }

        private string CreateTempFile()
        {
            var path = Path.GetTempFileName();
            _tempFiles.Add(path);
            return path;
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

        [Fact]
        public void SaveToFile_WritesCorrectContent()
        {
            var filePath = CreateTempFile();
            var collisions = _generator.Generate(0, 1, 0, 0, 1.5);

            _generator.SaveToFile(filePath, collisions);

            var lines = File.ReadAllLines(filePath);
            Assert.Equal(collisions.Count, lines.Length);
            Assert.Equal("dx,dy,ddx,ddy", lines[0]);
        }

        [Fact]
        public void LoadFromFile_ReturnsCorrectHashSet()
        {
            var filePath = CreateTempFile();
            File.WriteAllLines(filePath, new[] { "dx,dy,ddx,ddy", "1,2,3,4", "5,6,7,8" });

            var result = _generator.LoadFromFile(filePath);

            Assert.Contains((1, 2, 3, 4), result);
            Assert.Contains((5, 6, 7, 8), result);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void SaveAndLoad_RoundTrip_DataPreserved()
        {
            var filePath = CreateTempFile();
            var collisions = _generator.Generate(-1, 1, -1, 1, 1.5);

            _generator.SaveToFile(filePath, collisions);
            var loaded = _generator.LoadFromFile(filePath);

            collisions.Skip(1).ToList().ForEach(line =>
            {
                var parts = line.Split(',');
                Assert.Contains((
                    int.Parse(parts[0]),
                    int.Parse(parts[1]),
                    int.Parse(parts[2]),
                    int.Parse(parts[3])), loaded);
            });

            Assert.Equal(collisions.Count - 1, loaded.Count);
        }

        [Fact]
        public void LoadFromFile_HeaderOnly_ReturnsEmptyHashSet()
        {
            var filePath = CreateTempFile();
            File.WriteAllLines(filePath, new[] { "dx,dy,ddx,ddy" });

            var result = _generator.LoadFromFile(filePath);

            Assert.Empty(result);
        }

        [Fact]
        public void LoadFromFile_NonExistentFile_ThrowsException()
        {
            Assert.Throws<FileNotFoundException>(() => _generator.LoadFromFile("nonexistent_file_12345.csv"));
        }
    }
}
