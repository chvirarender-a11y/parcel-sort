using System.Collections.Generic;
using ParcelSort.Core;
using ParcelSort.Game.World;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ParcelSort.Game
{
    /// <summary>
    /// Runs a level: loads levels from Resources/Levels, applies taps to the HarvestState,
    /// hands the results to the 3D farm to animate, and draws the HUD and result panels.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        const string ProgressKey = "Harvest.Level";

        static readonly Color Ink = new Color(0.24f, 0.22f, 0.20f);
        static readonly Color Soft = new Color(0.50f, 0.48f, 0.45f);
        static readonly Color Paper = new Color(1f, 0.99f, 0.96f, 0.94f);
        static readonly Color Track = new Color(0.90f, 0.88f, 0.82f);
        static readonly Color Progress = new Color(0.47f, 0.80f, 0.42f);
        static readonly Color UndoColor = new Color(0.45f, 0.66f, 0.95f);
        static readonly Color MergeColor = new Color(0.98f, 0.64f, 0.33f);
        static readonly Color RestartColor = new Color(0.62f, 0.62f, 0.70f);
        static readonly Color GoColor = new Color(0.40f, 0.76f, 0.42f);
        static readonly Color StarOn = new Color(1f, 0.80f, 0.25f);
        static readonly Color StarOff = new Color(0.86f, 0.84f, 0.80f);

        readonly List<FieldLevel> levels = new List<FieldLevel>();
        int levelIndex;
        int totalCrops;
        HarvestState state;
        FarmBoard board;
        RectTransform hud;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // Press Play in any scene, even an empty one, and the game builds itself.
            if (FindAnyObjectByType<GameController>() == null) new GameObject("Game").AddComponent<GameController>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;

            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            board = gameObject.AddComponent<FarmBoard>();
            board.Tapped += OnTap;
            board.Held += OnHold;
            board.Settled += DrawHud;
            hud = Ui.Fill("HUD", Ui.CreateCanvas("Canvas").transform);

            foreach (var asset in Resources.LoadAll<TextAsset>("Levels"))
            {
                var level = FieldLevel.FromJson(asset.text);
                var errors = level.Validate();
                if (errors.Count > 0) Debug.LogError(asset.name + ": " + string.Join("; ", errors));
                else levels.Add(level);
            }
            levels.Sort((a, b) => a.id.CompareTo(b.id));
            if (levels.Count == 0)
            {
                Debug.LogError("No levels found in Assets/Resources/Levels");
                return;
            }

            StartLevel(PlayerPrefs.GetInt(ProgressKey, 0) % levels.Count);
        }

        void StartLevel(int index)
        {
            levelIndex = index;
            state = new HarvestState(levels[index]);
            totalCrops = state.CropsLeft;
            board.BuildLevel(state);
            DrawHud();
        }

        void OnTap(Move move)
        {
            if (state.Status != GameStatus.Playing) return;
            if (move.FromBay && !board.CanTapBay(move.Index)) return;
            var lap = Solver.Apply(state, move);
            if (lap == null) return;
            if (state.Status == GameStatus.Won)
            {
                PlayerPrefs.SetInt(ProgressKey, (levelIndex + 1) % levels.Count);
                PlayerPrefs.Save();
            }
            board.Launch(lap);
            DrawHud();
        }

        void OnHold(Move move)
        {
            if (move.FromBay && !board.CanTapBay(move.Index)) return;
            board.ShowPreview(state.Preview(move.FromBay, move.Index));
        }

        /// <summary>For changes that rebuild the farm without animation: undo, merge, revive.</summary>
        void ApplyInstantly(bool changed)
        {
            if (!changed) return;
            board.Refresh(state);
            DrawHud();
        }

        void DrawHud()
        {
            Ui.Clear(hud);
            bool idle = board.Idle;

            Ui.Box(hud, Paper, 0, 0, 1080, 260, "Top bar");
            var level = state.Level;
            Ui.Label(hud, "LEVEL " + level.id, 30, Soft, 44, 34, 520, 44, TextAnchor.MiddleLeft);
            Ui.Label(hud, level.name, 60, Ink, 40, 74, 560, 80, TextAnchor.MiddleLeft);

            var undo = Ui.TextButton(hud, "Undo " + state.UndosLeft, UndoColor, 560, 44, 160, 100,
                () => ApplyInstantly(board.Idle && state.Undo()));
            undo.interactable = idle && state.CanUndo && state.Status != GameStatus.Won;
            var merge = Ui.TextButton(hud, "Merge " + state.MergesLeft, MergeColor, 732, 44, 170, 100,
                () => ApplyInstantly(board.Idle && state.MergeBays().Count > 0));
            merge.interactable = idle && state.CanMerge;
            Ui.TextButton(hud, "Reset", RestartColor, 914, 44, 120, 100, () => StartLevel(levelIndex));

            int harvested = totalCrops - state.CropsLeft;
            Ui.Box(hud, Track, 44, 176, 992, 34, "Progress track");
            if (harvested > 0) Ui.Box(hud, Progress, 44, 176, 992f * harvested / totalCrops, 34, "Progress");
            Ui.Label(hud, harvested + " / " + totalCrops, 26, Ink, 44, 176, 992, 34);

            if (level.id <= 2 && state.Moves == 0)
                Ui.Label(hud, "Tap a harvester to send it round the field.  Hold to preview.", 30, Soft, 0, 270, 1080, 50);

            if (state.Status != GameStatus.Playing && idle) DrawResult();
        }

        void DrawResult()
        {
            Ui.FillBox(hud, new Color(0.15f, 0.20f, 0.15f, 0.45f), "Shade");
            var panel = Ui.Box(hud, new Color(1f, 0.99f, 0.96f), 120, 600, 840, 700, "Result").transform;

            if (state.Status == GameStatus.Won)
            {
                Ui.Label(panel, "Harvest complete!", 64, Ink, 0, 60, 840, 100);
                for (int i = 0; i < 3; i++)
                {
                    var star = Ui.Box(panel, i < state.Stars ? StarOn : StarOff, 250 + i * 130, 210, 90, 90, "Star");
                    star.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                }
                string note = state.Revived ? "Revived once" : state.UndoUsed ? "Used undo" : "Perfect run!";
                Ui.Label(panel, note + "   ·   best combo " + state.BestCombo, 34, Soft, 0, 350, 840, 60);
                Ui.TextButton(panel, "Next field", GoColor, 220, 480, 400, 130, () => StartLevel((levelIndex + 1) % levels.Count));
                return;
            }

            Ui.Label(panel, "No free bay!", 64, Ink, 0, 60, 840, 100);
            Ui.Label(panel, "A harvester still had room but every bay was taken.", 32, Soft, 40, 170, 760, 60);
            float x = 60;
            if (!state.Revived)
            {
                // Rewarded ad goes here later. No interstitials on failure, by design.
                Ui.TextButton(panel, "+1 Bay", GoColor, x, 300, 230, 130, () => ApplyInstantly(state.Revive()));
                x += 250;
            }
            if (state.CanUndo)
            {
                Ui.TextButton(panel, "Undo", UndoColor, x, 300, 230, 130, () => ApplyInstantly(state.Undo()));
                x += 250;
            }
            Ui.TextButton(panel, "Retry", RestartColor, x, 300, 230, 130, () => StartLevel(levelIndex));
            Ui.Label(panel, "+1 Bay: watch a short video (coming soon)", 28, Soft, 0, 470, 840, 50);
        }
    }
}
