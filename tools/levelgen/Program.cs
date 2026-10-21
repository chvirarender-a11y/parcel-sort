// Generates Assets/Resources/Levels/level_XXX.json from the hand-drawn fields below.
// Usage (from the project root): ./tools/gen-levels.sh
using System;
using System.IO;
using ParcelSort.Core;

static class Program
{
    // Letters: r tomato, g cabbage, b blueberry, y corn, p eggplant, o pumpkin, w cauliflower, k radish.
    static readonly object[][] Fields =
    {
        new object[] { "Heart", new[] { ".r.r.", "rrrrr", "rrrrr", ".rrr.", "..r.." } },
        new object[] { "Tomato", new[] { "..g..", ".rgr.", "rrrrr", "rrrrr", ".rrr." } },
        new object[] { "Corn", new[] { "..yy..", ".yyyy.", "gyyyyg", "gyyyyg", ".gyyg.", "..gg.." } },
        new object[] { "Mushroom", new[] { ".rrrr.", "rrwrrr", "rrrrwr", ".wwww.", "..ww..", "..ww.." } },
        new object[] { "Carrot", new[] { "..g.g..", "..ggg..", "..ooo..", "..ooo..", "..ooo..", "...oo..", "...o..." } },
        new object[] { "Flower", new[] { ".pp.pp.", "ppyyypp", ".pyyyp.", "ppyyypp", ".pp.pp.", "...g...", "..ggg.." } },
        new object[] { "Sun", new[] { "y..y..y", ".yoooy.", ".ooooo.", "yoooooy", ".ooooo.", ".yoooy.", "y..y..y" } },
        new object[] { "Apple Tree", new[] { "..gggg..", ".ggrggg.", "gggggrgg", "grgggggg", ".gggrgg.", "...oo...", "...oo...", "..oooo.." } },
        new object[] { "Pumpkin", new[] { "...gg...", ".oooooo.", "oooyyooo", "ooyooyoo", "oooooooo", "oooyyooo", ".oooooo." } },
        new object[] { "Cat", new[] { "o......o", "oo....oo", "oooooooo", "owkoowko", "oooooooo", "oowkkwoo", ".oooooo.", "..o..o.." } },
        new object[] { "Strawberry", new[] { "..g..g..", ".gggggg.", "rrrrrrrr", "ryrrryrr", "rrryrrrr", ".rrrryr.", "..rrrr..", "...rr..." } },
        new object[] { "Sunflower", new[] { "..yyyy..", ".yyooyy.", "yyokkoyy", "yokppkoy", "yokppkoy", "yyokkoyy", ".yyooyy.", "..yyyy.." } },
        new object[] { "Cabbage", new[] { ".gggggg.", "ggwwwwgg", "gwggggwg", "gwgwwgwg", "gwgwwgwg", "gwggggwg", "ggwwwwgg", ".gggggg." } },
        new object[] { "Rainbow", new[] { "..rrrrr..", ".rooooor.", "royyyyyor", "roygggyor", "roygbgyor", "roygbgyor" } },
        new object[] { "Berry Pie", new[] { "...ooo...", ".ooooooo.", "oobbbbboo", "obbpppbbo", "obpwwwpbo", "obbpppbbo", "oobbbbboo", ".ooooooo.", "...ooo..." } },
    };

    static int Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : "Assets/Resources/Levels";
        Directory.CreateDirectory(dir);

        for (int i = 0; i < Fields.Length; i++)
        {
            double t = i / (double)(Fields.Length - 1);
            var settings = new LevelGenerator.Settings
            {
                Lanes = i < 8 ? 2 : 3,
                Bays = i < 4 ? 5 : i < 8 ? 3 : 2,
                MinCapacity = 2,
                MaxCapacity = 5 + i / 4,
                TargetWinRate = 0.95 - 0.7 * t,
            };
            var level = LevelGenerator.Generate(i + 1, (string)Fields[i][0], (string[])Fields[i][1], settings, 1000 + i);
            if (level == null)
            {
                Console.Error.WriteLine("No solvable deal for " + Fields[i][0]);
                return 1;
            }
            double rate = Solver.RandomWinRate(level, 200, 7);
            File.WriteAllText(Path.Combine(dir, string.Format("level_{0:000}.json", i + 1)), level.ToJson());
            Console.WriteLine("{0,2} {1,-11} {2}x{3} lanes {4} win rate {5:P0}", level.id, level.name, level.Width, level.Height, level.lanes.Length, rate);
        }
        return 0;
    }
}
