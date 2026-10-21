using System;
using System.Collections.Generic;

namespace ParcelSort.Core
{
    /// <summary>A dispatch: the front harvester of a lane, or a parked harvester in a bay.</summary>
    public struct Move
    {
        public bool FromBay;
        public int Index;

        public Move(bool fromBay, int index)
        {
            FromBay = fromBay;
            Index = index;
        }

        public override string ToString()
        {
            return (FromBay ? "bay " : "lane ") + Index;
        }
    }

    /// <summary>Checks that levels can be won and estimates how hard they feel.</summary>
    public static class Solver
    {
        /// <summary>
        /// Moves worth trying from a position. Sending a parked harvester that would pick nothing
        /// just parks it again, so those are skipped.
        /// </summary>
        public static List<Move> UsefulMoves(HarvestState state)
        {
            var moves = new List<Move>();
            if (state.Status != GameStatus.Playing) return moves;
            for (int bay = 0; bay < state.BayCount; bay++)
            {
                if (!state.CanDispatchBay(bay)) continue;
                var lap = state.Preview(true, bay);
                if (lap.Picks.Count > 0) moves.Add(new Move(true, bay));
            }
            for (int lane = 0; lane < state.LaneCount; lane++)
                if (state.CanDispatchLane(lane)) moves.Add(new Move(false, lane));
            return moves;
        }

        public static LapResult Apply(HarvestState state, Move move)
        {
            return move.FromBay ? state.DispatchBay(move.Index) : state.DispatchLane(move.Index);
        }

        /// <summary>A winning sequence of moves, or null if none was found within the node budget.</summary>
        public static List<Move> Solve(FieldLevel level, int maxNodes = 200000)
        {
            var seen = new HashSet<string>();
            var path = new List<Move>();
            int nodes = 0;
            return Search(new HarvestState(level), seen, path, ref nodes, maxNodes) ? path : null;
        }

        static bool Search(HarvestState state, HashSet<string> seen, List<Move> path, ref int nodes, int maxNodes)
        {
            if (state.Status == GameStatus.Won) return true;
            if (state.Status == GameStatus.Lost || ++nodes > maxNodes) return false;
            if (!seen.Add(state.Key())) return false;

            foreach (var move in UsefulMoves(state))
            {
                var next = state.Clone();
                Apply(next, move);
                path.Add(move);
                if (Search(next, seen, path, ref nodes, maxNodes)) return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }

        /// <summary>
        /// Share of games a careless player wins: each turn they pick a random useful move.
        /// Higher is easier.
        /// </summary>
        public static double RandomWinRate(FieldLevel level, int games, int seed)
        {
            var rng = new Random(seed);
            int wins = 0;
            for (int g = 0; g < games; g++)
            {
                var state = new HarvestState(level);
                while (state.Status == GameStatus.Playing)
                {
                    var moves = UsefulMoves(state);
                    if (moves.Count == 0) break;
                    Apply(state, moves[rng.Next(moves.Count)]);
                }
                if (state.Status == GameStatus.Won) wins++;
            }
            return (double)wins / games;
        }
    }
}
