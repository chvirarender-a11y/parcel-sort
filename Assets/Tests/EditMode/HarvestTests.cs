using System.IO;
using NUnit.Framework;
using ParcelSort.Core;

namespace ParcelSort.Tests
{
    public class HarvestTests
    {
        static FieldLevel Level(string[] field, string[] lanes, int bays = 5)
        {
            return new FieldLevel { id = 1, name = "Test", bays = bays, field = field, lanes = lanes };
        }

        [Test]
        public void LapPicksOutermostMatchingCropsUntilFull()
        {
            // y = 1: g r
            // y = 0: r r
            var state = new HarvestState(Level(new[] { "gr", "rr" }, new[] { "r3", "g1" }));
            var lap = state.DispatchLane(0);

            Assert.AreEqual(3, lap.Picks.Count);
            Assert.AreEqual(0, lap.Picks[0].X); Assert.AreEqual(0, lap.Picks[0].Y);
            Assert.AreEqual(1, lap.Picks[1].X); Assert.AreEqual(0, lap.Picks[1].Y);
            Assert.AreEqual(1, lap.Picks[2].X); Assert.AreEqual(1, lap.Picks[2].Y);
            Assert.IsTrue(lap.Filled);
            Assert.AreEqual(3, lap.EndStation, "fills up at the right side, row 1");
            Assert.AreEqual(-1, lap.ParkedBay);
            Assert.AreEqual(1, state.CropsLeft);
        }

        [Test]
        public void BuriedCropsAreNotPicked()
        {
            var state = new HarvestState(Level(new[] { "ggg", "grg", "ggg" }, new[] { "r1", "g8" }));
            var lap = state.DispatchLane(0);

            Assert.AreEqual(0, lap.Picks.Count);
            Assert.AreEqual(0, lap.ParkedBay);
            Assert.AreEqual(1, state.Bay(0).Capacity);
        }

        [Test]
        public void MergeBoosterJoinsParkedHarvestersOfOneColor()
        {
            var state = new HarvestState(Level(new[] { "ggg", "grg", "ggg" }, new[] { "r1", "r2", "g5" }));
            state.DispatchLane(0);
            var second = state.DispatchLane(1);
            Assert.AreEqual(1, second.ParkedBay);
            Assert.IsTrue(state.CanMerge);

            var freed = state.MergeBays();
            Assert.AreEqual(1, freed.Count);
            Assert.AreEqual(3, state.Bay(0).Capacity);
            Assert.AreEqual(state.BayCount - 1, state.FreeBays);
            Assert.IsFalse(state.CanMerge, "one merge per level");
        }

        [Test]
        public void OverflowLosesAndReviveAddsABay()
        {
            var state = new HarvestState(Level(new[] { "ggg", "grg", "ggg" }, new[] { "r1", "g8" }, bays: 1));
            state.DispatchLane(0);
            state.DispatchBay(0);
            Assert.AreEqual(GameStatus.Playing, state.Status, "sending a parked harvester always finds its bay again");

            state = new HarvestState(Level(new[] { "ggg", "grg", "ggg" }, new[] { "r1", "b1" }, bays: 1));
            state.DispatchLane(0);
            var lost = state.DispatchLane(1);
            Assert.IsTrue(lost.Overflow);
            Assert.AreEqual(GameStatus.Lost, state.Status);

            Assert.IsTrue(state.Revive());
            Assert.AreEqual(GameStatus.Playing, state.Status);
            Assert.AreEqual(2, state.BayCount);
            Assert.AreEqual(1, state.Bay(1).Capacity);
            Assert.IsFalse(state.Revive(), "only one revive");
        }

        [Test]
        public void UndoRestoresTheFieldAndIsLimited()
        {
            var state = new HarvestState(Level(new[] { "rgr" }, new[] { "r2", "g1" }));
            Assert.IsFalse(state.CanUndo);
            state.DispatchLane(0);
            Assert.AreEqual(1, state.CropsLeft);
            Assert.IsTrue(state.Undo());
            Assert.AreEqual(3, state.CropsLeft);
            Assert.IsFalse(state.LaneEmpty(0));
            Assert.IsTrue(state.UndoUsed);

            for (int i = 1; i < HarvestState.FreeUndos; i++)
            {
                state.DispatchLane(1);
                Assert.IsTrue(state.Undo());
            }
            state.DispatchLane(1);
            Assert.IsFalse(state.Undo(), "free undos are used up");
        }

        [Test]
        public void ClearingTheFieldWinsWithStars()
        {
            var state = new HarvestState(Level(new[] { "rgr" }, new[] { "g1", "r2" }));
            state.DispatchLane(1);
            state.DispatchLane(0);
            Assert.AreEqual(GameStatus.Won, state.Status);
            Assert.AreEqual(3, state.Stars);
        }

        [Test]
        public void ComboNeedsFivePicksInOneLap()
        {
            var state = new HarvestState(Level(new[] { "rrr", "rrr" }, new[] { "r6" }));
            var lap = state.DispatchLane(0);
            Assert.IsTrue(lap.Combo);
            Assert.AreEqual(GameStatus.Won, state.Status);
        }

        [Test]
        public void PreviewDoesNotChangeTheState()
        {
            var state = new HarvestState(Level(new[] { "rgr" }, new[] { "r2", "g1" }));
            var lap = state.Preview(false, 0);
            Assert.AreEqual(2, lap.Picks.Count);
            Assert.AreEqual(3, state.CropsLeft);
            Assert.IsFalse(state.CanUndo);
        }

        [Test]
        public void SolverFindsWinsAndRejectsDeadLevels()
        {
            var level = Level(new[] { "ggg", "grg", "ggg" }, new[] { "r1 g4", "g4" }, bays: 1);
            var solution = Solver.Solve(level);
            Assert.IsNotNull(solution);
            var state = new HarvestState(level);
            foreach (var move in solution) Solver.Apply(state, move);
            Assert.AreEqual(GameStatus.Won, state.Status);

            var dead = Level(new[] { "ggg", "grg", "ggg" }, new[] { "r1 g8" }, bays: 0);
            Assert.IsNull(Solver.Solve(dead));
        }

        [Test]
        public void ValidateChecksCropsAgainstCapacity()
        {
            Assert.AreEqual(0, Level(new[] { "rg" }, new[] { "r1 g1" }).Validate().Count);
            Assert.AreEqual(1, Level(new[] { "rg" }, new[] { "r2 g1" }).Validate().Count);
            Assert.IsTrue(Level(new[] { "rg", "r" }, new[] { "r2 g1" }).Validate().Count > 0, "ragged rows");
        }

        [Test]
        public void JsonRoundTrip()
        {
            var level = Level(new[] { ".r.", "rgr" }, new[] { "r3", "g1" }, bays: 4);
            var copy = FieldLevel.FromJson(level.ToJson());
            Assert.AreEqual(level.ToJson(), copy.ToJson());
            Assert.AreEqual(4, copy.bays);
            Assert.AreEqual(Crops.None, copy.CropAt(0, 1));
            Assert.AreEqual(Crops.FromLetter('g'), copy.CropAt(1, 0));
        }

        [Test]
        public void GeneratorMakesSolvableLevels()
        {
            var field = new[] { ".rr.", "rggr", "rggr", ".rr." };
            var level = LevelGenerator.Generate(7, "Ring", field, new LevelGenerator.Settings { Lanes = 2, MinCapacity = 2, MaxCapacity = 4, Attempts = 5 }, 1);
            Assert.IsNotNull(level);
            Assert.AreEqual(0, level.Validate().Count);
            Assert.IsNotNull(Solver.Solve(level));
        }

        [Test]
        public void ShippedLevelsAreValidAndWinnable()
        {
            var files = Directory.GetFiles("Assets/Resources/Levels", "level_*.json");
            Assert.IsTrue(files.Length >= 15, "levels found: " + files.Length);
            foreach (var file in files)
            {
                var level = FieldLevel.FromJson(File.ReadAllText(file));
                Assert.AreEqual(0, level.Validate().Count, file);
                Assert.IsNotNull(Solver.Solve(level), file + " cannot be won");
            }
        }
    }
}
