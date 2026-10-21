using System;
using System.Collections.Generic;

namespace ParcelSort.Core
{
    public class SolveResult
    {
        public bool Solvable;
        /// <summary>Stack indices to tap, in order. Empty when not solvable.</summary>
        public List<int> Solution = new List<int>();
        public int NodesVisited;
        public bool HitNodeLimit;
    }

    /// <summary>
    /// Depth-first search over positions with a memo of dead ends. The solver sees hidden
    /// parcels, so it answers "can this level be won at all", not "can a player read it".
    /// </summary>
    public static class Solver
    {
        public static SolveResult Solve(LevelData level, int nodeLimit = 2000000)
        {
            var result = new SolveResult();
            var start = new GameState(level) { RecordHistory = false };
            var deadEnds = new HashSet<string>();
            result.Solvable = Search(start, deadEnds, result, nodeLimit);
            if (!result.Solvable) result.Solution.Clear();
            return result;
        }

        static bool Search(GameState state, HashSet<string> deadEnds, SolveResult result, int nodeLimit)
        {
            if (state.Status == GameStatus.Won) return true;
            if (state.Status == GameStatus.Lost) return false;
            if (++result.NodesVisited > nodeLimit)
            {
                result.HitNodeLimit = true;
                return false;
            }

            string key = state.PositionKey();
            if (deadEnds.Contains(key)) return false;

            // Try taps that go straight onto a truck first; they are almost always good moves.
            var order = new List<int>();
            var later = new List<int>();
            for (int s = 0; s < state.Stacks.Count; s++)
            {
                if (!state.CanTap(s)) continue;
                int color = state.Stacks[s][state.Stacks[s].Count - 1].Color;
                bool fits = false;
                foreach (var dock in state.Docks) if (dock.Color == color && dock.Load < state.TruckCapacity) fits = true;
                (fits ? order : later).Add(s);
            }
            order.AddRange(later);

            foreach (int s in order)
            {
                var next = state.Clone();
                next.Tap(s);
                result.Solution.Add(s);
                if (Search(next, deadEnds, result, nodeLimit)) return true;
                result.Solution.RemoveAt(result.Solution.Count - 1);
                if (result.HitNodeLimit) return false;
            }

            deadEnds.Add(key);
            return false;
        }

        /// <summary>
        /// Share of random playouts that win. Used as a rough difficulty score:
        /// a level where random tapping often wins is easy.
        /// </summary>
        public static double RandomWinRate(LevelData level, int playouts, int seed)
        {
            var rng = new Random(seed);
            int wins = 0;
            var choices = new List<int>();
            for (int p = 0; p < playouts; p++)
            {
                var state = new GameState(level) { RecordHistory = false };
                while (state.Status == GameStatus.Playing)
                {
                    choices.Clear();
                    for (int s = 0; s < state.Stacks.Count; s++) if (state.CanTap(s)) choices.Add(s);
                    state.Tap(choices[rng.Next(choices.Count)]);
                }
                if (state.Status == GameStatus.Won) wins++;
            }
            return (double)wins / playouts;
        }
    }
}
