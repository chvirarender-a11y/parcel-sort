using System.IO;
using ParcelSort.Core;
using UnityEditor;
using UnityEngine;

namespace ParcelSort.Editor
{
    public static class LevelTools
    {
        const string LevelFolder = "Assets/Resources/Levels";

        [MenuItem("Harvest/Check All Levels")]
        static void CheckAllLevels()
        {
            int bad = 0;
            foreach (var file in Directory.GetFiles(LevelFolder, "level_*.json"))
            {
                var level = FieldLevel.FromJson(File.ReadAllText(file));
                var errors = level.Validate();
                if (errors.Count > 0)
                {
                    bad++;
                    Debug.LogError(file + ": " + string.Join("; ", errors));
                    continue;
                }
                var solution = Solver.Solve(level);
                if (solution == null)
                {
                    bad++;
                    Debug.LogError(file + ": cannot be solved");
                    continue;
                }
                Debug.Log(string.Format("{0}: OK, {1} taps, random win rate {2:P0}",
                    Path.GetFileName(file), solution.Count, Solver.RandomWinRate(level, 200, 1)));
            }
            Debug.Log(bad == 0 ? "All levels OK" : bad + " level(s) have problems");
        }

        [MenuItem("Harvest/Reset Progress")]
        static void ResetProgress()
        {
            PlayerPrefs.DeleteKey("Harvest.Level");
            Debug.Log("Progress reset to level 1");
        }
    }
}
