using System.IO;
using NUnit.Framework;
using ParcelSort.Core;

namespace ParcelSort.Tests
{
    public class LevelTests
    {
        const string LevelFolder = "Assets/Resources/Levels";

        [Test]
        public void JsonRoundTripKeepsEveryField()
        {
            var level = new LevelData { id = 7, bufferSize = 4, dockCount = 3, truckCapacity = 3, trucks = "rgb", stacks = new[] { "rGb", "", "Bgr" } };
            var copy = LevelData.FromJson(level.ToJson());
            Assert.AreEqual(level.ToJson(), copy.ToJson());
        }

        [Test]
        public void ValidateCatchesWrongParcelCount()
        {
            var level = new LevelData { trucks = "rg", stacks = new[] { "rrr", "gg" } };
            Assert.AreEqual(1, level.Validate().Count);
        }

        [Test]
        public void SolverFindsAWinningSequence()
        {
            var level = new LevelData { bufferSize = 3, dockCount = 1, truckCapacity = 3, trucks = "rg", stacks = new[] { "grr", "rgg" } };
            var result = Solver.Solve(level);
            Assert.IsTrue(result.Solvable);

            var state = new GameState(level);
            foreach (int stack in result.Solution) state.Tap(stack);
            Assert.AreEqual(GameStatus.Won, state.Status);
        }

        [Test]
        public void SolverReportsUnwinnableLevel()
        {
            // One dock, one buffer slot: the green on top must wait in the buffer, then the next green fills it.
            var level = new LevelData { bufferSize = 1, dockCount = 1, truckCapacity = 3, trucks = "rg", stacks = new[] { "rrrggg" } };
            Assert.IsFalse(Solver.Solve(level).Solvable);
        }

        [Test]
        public void GeneratorProducesSolvableLevels()
        {
            var settings = new GeneratorSettings { colors = 3, trucksPerColor = 2, stackCount = 4, hiddenChance = 0.3 };
            var generated = LevelGenerator.Generate(settings, 42);
            Assert.IsNotNull(generated);
            Assert.AreEqual(0, generated.Level.Validate().Count);
            Assert.IsTrue(generated.Solve.Solvable);
        }

        [Test]
        public void ShippedLevelsAreValidAndSolvable()
        {
            var files = Directory.GetFiles(LevelFolder, "level_*.json");
            Assert.IsTrue(files.Length > 0, "no level files found in " + LevelFolder);
            foreach (var file in files)
            {
                var level = LevelData.FromJson(File.ReadAllText(file));
                var errors = level.Validate();
                Assert.AreEqual(0, errors.Count, file + ": " + string.Join("; ", errors.ToArray()));
                Assert.IsTrue(Solver.Solve(level).Solvable, file + " cannot be solved");
            }
        }
    }
}
