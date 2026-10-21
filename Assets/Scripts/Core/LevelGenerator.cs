using System;
using System.Collections.Generic;
using System.Text;

namespace ParcelSort.Core
{
    [Serializable]
    public class GeneratorSettings
    {
        public int colors = 3;
        public int trucksPerColor = 2;
        public int stackCount = 4;
        public int bufferSize = 5;
        public int dockCount = 2;
        public int truckCapacity = 3;
        /// <summary>Chance that a parcel below the top of its stack starts hidden.</summary>
        public double hiddenChance;
        /// <summary>Accept only levels whose random-play win rate falls in this range.</summary>
        public double minRandomWinRate;
        public double maxRandomWinRate = 1.0;
    }

    public class GeneratedLevel
    {
        public LevelData Level;
        public SolveResult Solve;
        public double RandomWinRate;
        public int Attempts;
    }

    /// <summary>
    /// Makes random candidate levels and keeps the first one the solver can win
    /// whose difficulty is in the requested range.
    /// </summary>
    public static class LevelGenerator
    {
        public static GeneratedLevel Generate(GeneratorSettings settings, int seed, int maxAttempts = 500)
        {
            var rng = new Random(seed);
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var level = MakeCandidate(settings, rng);
                var solve = Solver.Solve(level, 200000);
                if (!solve.Solvable) continue;
                double winRate = Solver.RandomWinRate(level, 200, seed + attempt);
                if (winRate < settings.minRandomWinRate || winRate > settings.maxRandomWinRate) continue;
                return new GeneratedLevel { Level = level, Solve = solve, RandomWinRate = winRate, Attempts = attempt };
            }
            return null;
        }

        public static LevelData MakeCandidate(GeneratorSettings settings, Random rng)
        {
            var colorPool = new List<int>();
            for (int c = 0; c < ParcelColors.Count; c++) colorPool.Add(c);
            Shuffle(colorPool, rng);
            colorPool.RemoveRange(settings.colors, colorPool.Count - settings.colors);

            var trucks = new List<int>();
            var parcels = new List<int>();
            foreach (int color in colorPool)
            {
                for (int t = 0; t < settings.trucksPerColor; t++)
                {
                    trucks.Add(color);
                    for (int p = 0; p < settings.truckCapacity; p++) parcels.Add(color);
                }
            }
            Shuffle(trucks, rng);
            Shuffle(parcels, rng);

            // Deal parcels round-robin so stacks have similar heights, then shift a few for variety.
            var stacks = new List<StringBuilder>();
            for (int s = 0; s < settings.stackCount; s++) stacks.Add(new StringBuilder());
            for (int i = 0; i < parcels.Count; i++) stacks[i % settings.stackCount].Append(ParcelColors.ToLetter(parcels[i]));
            for (int i = 0; i < settings.stackCount; i++)
            {
                var from = stacks[rng.Next(stacks.Count)];
                var to = stacks[rng.Next(stacks.Count)];
                if (from == to || from.Length <= 2) continue;
                to.Append(from[from.Length - 1]);
                from.Length--;
            }

            var level = new LevelData
            {
                bufferSize = settings.bufferSize,
                dockCount = settings.dockCount,
                truckCapacity = settings.truckCapacity,
                stacks = new string[stacks.Count],
            };
            var truckLetters = new StringBuilder();
            foreach (int t in trucks) truckLetters.Append(ParcelColors.ToLetter(t));
            level.trucks = truckLetters.ToString();

            for (int s = 0; s < stacks.Count; s++)
            {
                var chars = stacks[s].ToString().ToCharArray();
                for (int i = 0; i < chars.Length - 1; i++)
                {
                    if (rng.NextDouble() < settings.hiddenChance) chars[i] = char.ToUpperInvariant(chars[i]);
                }
                level.stacks[s] = new string(chars);
            }
            return level;
        }

        static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
