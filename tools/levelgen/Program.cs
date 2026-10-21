// Generates the first 15 levels into Assets/Resources/Levels (see tools/gen-levels.sh).
// Inside Unity the same generator is available from the menu: Parcel Sort > Generate Level.
using System;
using System.IO;
using ParcelSort.Core;

static class Program
{
    // colors, trucks per color, stacks, min/max random win rate.
    // Difficulty follows an easy-easy-medium-easy-hard rhythm.
    static readonly double[][] Plan =
    {
        new double[] { 2, 1, 3, 0.90, 1.00 },
        new double[] { 3, 1, 3, 0.70, 1.00 },
        new double[] { 3, 2, 4, 0.50, 0.90 },
        new double[] { 3, 2, 4, 0.60, 1.00 },
        new double[] { 3, 2, 5, 0.30, 0.50 },
        new double[] { 4, 2, 5, 0.50, 0.80 },
        new double[] { 4, 2, 5, 0.40, 0.70 },
        new double[] { 4, 2, 6, 0.20, 0.40 },
        new double[] { 4, 2, 5, 0.40, 0.70 },
        new double[] { 4, 3, 6, 0.05, 0.20 },
        new double[] { 4, 3, 6, 0.30, 0.60 },
        new double[] { 4, 3, 6, 0.25, 0.50 },
        new double[] { 5, 2, 6, 0.10, 0.30 },
        new double[] { 4, 3, 6, 0.25, 0.50 },
        new double[] { 5, 3, 7, 0.02, 0.12 },
    };

    static int Main(string[] args)
    {
        string folder = args.Length > 0 ? args[0] : "Assets/Resources/Levels";
        Directory.CreateDirectory(folder);
        for (int i = 0; i < Plan.Length; i++)
        {
            var p = Plan[i];
            var settings = new GeneratorSettings
            {
                colors = (int)p[0],
                trucksPerColor = (int)p[1],
                stackCount = (int)p[2],
                minRandomWinRate = p[3],
                maxRandomWinRate = p[4],
            };
            GeneratedLevel generated = null;
            for (int seed = 1000 * (i + 1); generated == null && seed < 1000 * (i + 1) + 20; seed++)
                generated = LevelGenerator.Generate(settings, seed);
            if (generated == null)
            {
                Console.Error.WriteLine("level " + (i + 1) + ": no level found");
                return 1;
            }
            generated.Level.id = i + 1;
            string path = Path.Combine(folder, string.Format("level_{0:000}.json", i + 1));
            File.WriteAllText(path, generated.Level.ToJson());
            Console.WriteLine(string.Format("level {0,2}: random win rate {1:P0}, {2} moves, {3} attempts",
                i + 1, generated.RandomWinRate, generated.Solve.Solution.Count, generated.Attempts));
        }
        return 0;
    }
}
