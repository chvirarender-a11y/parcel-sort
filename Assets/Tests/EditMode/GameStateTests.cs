using NUnit.Framework;
using ParcelSort.Core;

namespace ParcelSort.Tests
{
    public class GameStateTests
    {
        static LevelData Level(string trucks, int buffer, params string[] stacks)
        {
            return new LevelData { id = 1, bufferSize = buffer, dockCount = 2, truckCapacity = 3, trucks = trucks, stacks = stacks };
        }

        [Test]
        public void FirstTrucksParkAtStart()
        {
            var state = new GameState(Level("rgb", 5, "rrr", "ggg", "bbb"));
            Assert.AreEqual(ParcelColors.FromLetter('r'), state.Docks[0].Color);
            Assert.AreEqual(ParcelColors.FromLetter('g'), state.Docks[1].Color);
            Assert.AreEqual(1, state.TrucksRemaining);
        }

        [Test]
        public void MatchingParcelGoesOntoTruck()
        {
            var state = new GameState(Level("rgb", 5, "rrr", "ggg", "bbb"));
            var move = state.Tap(0);
            Assert.AreEqual(0, move.ToDock);
            Assert.AreEqual(1, state.Docks[0].Load);
            Assert.AreEqual(0, state.Buffer.Count);
        }

        [Test]
        public void NonMatchingParcelGoesToBuffer()
        {
            var state = new GameState(Level("rgb", 5, "rrr", "ggg", "bbb"));
            var move = state.Tap(2);
            Assert.AreEqual(-1, move.ToDock);
            Assert.AreEqual(1, state.Buffer.Count);
            Assert.AreEqual(1, state.MaxBufferUsed);
        }

        [Test]
        public void FullTruckLeavesAndNextTruckTakesFromBuffer()
        {
            var state = new GameState(Level("rgb", 5, "rrr", "ggg", "bbb"));
            state.Tap(2);
            state.Tap(2);
            Assert.AreEqual(2, state.Buffer.Count);

            state.Tap(0);
            state.Tap(0);
            var move = state.Tap(0);

            Assert.AreEqual(1, move.Departures.Count);
            Assert.AreEqual(0, move.Departures[0]);
            Assert.AreEqual(ParcelColors.FromLetter('b'), state.Docks[0].Color);
            Assert.AreEqual(2, state.Docks[0].Load);
            Assert.AreEqual(2, move.BufferLoads);
            Assert.AreEqual(0, state.Buffer.Count);
        }

        [Test]
        public void ClearingEverythingWins()
        {
            var state = new GameState(Level("rg", 5, "rrr", "ggg"));
            for (int i = 0; i < 3; i++) state.Tap(0);
            for (int i = 0; i < 3; i++) state.Tap(1);
            Assert.AreEqual(GameStatus.Won, state.Status);
            Assert.AreEqual(3, state.Stars);
        }

        [Test]
        public void FullBufferLoses()
        {
            var state = new GameState(Level("rgb", 2, "rrr", "ggg", "bbb"));
            state.Tap(2);
            Assert.AreEqual(GameStatus.Playing, state.Status);
            state.Tap(2);
            Assert.AreEqual(GameStatus.Lost, state.Status);
            Assert.IsNull(state.Tap(0));
        }

        [Test]
        public void UndoRestoresPreviousPosition()
        {
            var state = new GameState(Level("rgb", 5, "rrr", "ggg", "bbb"));
            string before = state.PositionKey();
            state.Tap(2);
            Assert.IsTrue(state.Undo());
            Assert.AreEqual(before, state.PositionKey());
            Assert.AreEqual(3, state.Stacks[2].Count);
            Assert.IsFalse(state.Undo());
        }

        [Test]
        public void HiddenParcelIsRevealedWhenItReachesTheTop()
        {
            var state = new GameState(Level("rg", 5, "Grr", "rgg"));
            Assert.IsTrue(state.Stacks[0][0].Hidden);
            state.Tap(0);
            state.Tap(0);
            Assert.IsFalse(state.Stacks[0][0].Hidden);
        }

        [Test]
        public void ReviveReturnsBufferedParcelsToTheirStacks()
        {
            var state = new GameState(Level("rgb", 2, "rrr", "ggg", "bbb"));
            state.Tap(2);
            state.Tap(2);
            Assert.AreEqual(GameStatus.Lost, state.Status);

            Assert.IsTrue(state.Revive());
            Assert.AreEqual(GameStatus.Playing, state.Status);
            Assert.AreEqual(0, state.Buffer.Count);
            Assert.AreEqual(3, state.Stacks[2].Count);
        }

        [Test]
        public void ExtraSlotCanOnlyBeUsedOnce()
        {
            var state = new GameState(Level("rgb", 2, "rrr", "ggg", "bbb"));
            Assert.IsTrue(state.AddBufferSlot());
            Assert.AreEqual(3, state.BufferCapacity);
            Assert.IsFalse(state.AddBufferSlot());
        }
    }
}
