using System;
using System.Collections.Generic;

namespace ParcelSort.Core
{
    /// <summary>
    /// Turns a pixel-art field into a level: splits each crop color into harvesters, deals them into
    /// lanes, and keeps the solvable deal whose random-play win rate is closest to the target.
    /// </summary>
    public static class LevelGenerator
    {
        public class Settings
        {
            public int Lanes = 3;
            public int Bays = 5;
            public int MinCapacity = 4;
            public int MaxCapacity = 12;
            /// <summary>0..1 share of careless games won. Lower is harder.</summary>
            public double TargetWinRate = 0.5;
            public int Attempts = 40;
            public int PlayoutsPerAttempt = 40;
        }

        public static FieldLevel Generate(int id, string name, string[] field, Settings settings, int seed)
        {
            var rng = new Random(seed);
            FieldLevel best = null;
            double bestGap = double.MaxValue;

            for (int attempt = 0; attempt < settings.Attempts; attempt++)
            {
                var level = new FieldLevel { id = id, name = name, bays = settings.Bays, field = field };
                level.lanes = Deal(Split(level, settings, rng), settings.Lanes, rng);
                if (level.Validate().Count > 0 || Solver.Solve(level, 50000) == null) continue;

                double rate = Solver.RandomWinRate(level, settings.PlayoutsPerAttempt, rng.Next());
                double gap = Math.Abs(rate - settings.TargetWinRate);
                if (gap < bestGap)
                {
                    best = level;
                    bestGap = gap;
                }
            }
            return best;
        }

        static List<Harvester> Split(FieldLevel level, Settings settings, Random rng)
        {
            var counts = new int[Crops.Count];
            for (int y = 0; y < level.Height; y++)
                for (int x = 0; x < level.Width; x++)
                {
                    int crop = level.CropAt(x, y);
                    if (crop != Crops.None) counts[crop]++;
                }

            var result = new List<Harvester>();
            for (int color = 0; color < Crops.Count; color++)
            {
                int left = counts[color];
                while (left > 0)
                {
                    int size = Math.Min(left, rng.Next(settings.MinCapacity, settings.MaxCapacity + 1));
                    // Never leave a crumb smaller than the minimum; fold it into this harvester.
                    if (left - size > 0 && left - size < settings.MinCapacity) size = left;
                    result.Add(new Harvester(color, size));
                    left -= size;
                }
            }
            return result;
        }

        static string[] Deal(List<Harvester> harvesters, int laneCount, Random rng)
        {
            for (int i = harvesters.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = harvesters[i];
                harvesters[i] = harvesters[j];
                harvesters[j] = t;
            }
            laneCount = Math.Max(1, Math.Min(laneCount, harvesters.Count));
            var lanes = new List<Harvester>[laneCount];
            for (int i = 0; i < laneCount; i++) lanes[i] = new List<Harvester>();
            for (int i = 0; i < harvesters.Count; i++) lanes[i % laneCount].Add(harvesters[i]);

            var result = new string[laneCount];
            for (int i = 0; i < laneCount; i++) result[i] = FieldLevel.FormatLane(lanes[i]);
            return result;
        }
    }
}
