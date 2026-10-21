using System.Collections;
using System.Collections.Generic;
using ParcelSort.Core;
using ParcelSort.Game.World;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ParcelSort.Game
{
    /// <summary>
    /// Runs a level: loads levels from Resources/Levels, applies taps to the GameState,
    /// lets the 3D board animate them and draws the HUD (buttons, next trucks, results).
    /// </summary>
    public class GameController : MonoBehaviour
    {
        const string ProgressKey = "ParcelSort.Level";

        static readonly Color Ink = new Color(0.20f, 0.18f, 0.16f);
        static readonly Color Paper = new Color(0.97f, 0.95f, 0.90f);
        static readonly Color ButtonColor = new Color(0.25f, 0.45f, 0.75f);

        // Second channel besides color, for color-blind players.
        static readonly string[] ParcelSymbols = { "R", "G", "B", "Y", "P", "O", "C", "W" };

        readonly List<LevelData> levels = new List<LevelData>();
        int levelIndex;
        GameState state;
        Board3D board;
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

            board = gameObject.AddComponent<Board3D>();
            board.StackTapped += OnTapStack;
            hud = Ui.Fill("HUD", Ui.CreateCanvas("Canvas").transform);

            foreach (var asset in Resources.LoadAll<TextAsset>("Levels"))
            {
                var level = LevelData.FromJson(asset.text);
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
            state = new GameState(levels[index]);
            board.BuildLevel(state);
            DrawHud();
        }

        void OnTapStack(int stack)
        {
            if (board.Busy) return;
            var before = state.Clone();
            var move = state.Tap(stack);
            if (move == null) return;
            if (state.Status == GameStatus.Won)
            {
                PlayerPrefs.SetInt(ProgressKey, (levelIndex + 1) % levels.Count);
                PlayerPrefs.Save();
            }
            StartCoroutine(PlayMove(before, move));
        }

        IEnumerator PlayMove(GameState before, MoveResult move)
        {
            yield return board.PlayMove(before, move, state);
            DrawHud();
        }

        /// <summary>For actions that change the state without an animation (undo, boosters).</summary>
        void ApplyInstantly(bool changed)
        {
            if (!changed) return;
            board.Refresh(state);
            DrawHud();
        }

        void DrawHud()
        {
            Ui.Clear(hud);

            Ui.Label(hud, "Level " + state.Level.id, 56, Ink, 40, 50, 460, 100, TextAnchor.MiddleLeft);
            Ui.TextButton(hud, "Restart", ButtonColor, 520, 50, 170, 100, () => { if (!board.Busy) StartLevel(levelIndex); });
            var undo = Ui.TextButton(hud, "Undo", ButtonColor, 705, 50, 150, 100, () => ApplyInstantly(!board.Busy && state.Undo()));
            undo.interactable = state.CanUndo;
            var extra = Ui.TextButton(hud, "+1 Slot", ButtonColor, 870, 50, 170, 100, () => ApplyInstantly(!board.Busy && state.AddBufferSlot()));
            extra.interactable = !state.ExtraSlotUsed && state.Status == GameStatus.Playing;

            Ui.Label(hud, "Next", 36, Ink, 40, 175, 100, 60, TextAnchor.MiddleLeft);
            int shown = 0;
            for (int t = state.NextTruck; t < state.Level.trucks.Length && shown < 6; t++, shown++)
            {
                int color = ParcelColors.FromLetter(state.Level.trucks[t]);
                var next = Ui.Box(hud, Board3D.Palette[color], 140 + shown * 80, 180, 64, 50, "Next truck");
                Ui.Label(next.transform, ParcelSymbols[color], 30, Ink, 0, 0, 64, 50);
            }
            if (shown == 0) Ui.Label(hud, "-", 36, Ink, 140, 175, 100, 60, TextAnchor.MiddleLeft);

            Ui.Label(hud, "Shelf " + state.Buffer.Count + " / " + state.BufferCapacity, 36, Ink, 640, 175, 400, 60, TextAnchor.MiddleRight);

            if (state.Status != GameStatus.Playing) DrawResult();
        }

        void DrawResult()
        {
            Ui.FillBox(hud, new Color(0, 0, 0, 0.5f), "Shade");
            var panel = Ui.Box(hud, Paper, 140, 640, 800, 640, "Result");

            if (state.Status == GameStatus.Won)
            {
                Ui.Label(panel.transform, "Level complete!", 64, Ink, 0, 60, 800, 100);
                Ui.Label(panel.transform, "Stars: " + state.Stars + " / 3", 52, Ink, 0, 190, 800, 80);
                Ui.Label(panel.transform, state.Moves + " taps, shelf peak " + state.MaxBufferUsed, 36, Ink, 0, 280, 800, 60);
                Ui.TextButton(panel.transform, "Next level", ButtonColor, 200, 420, 400, 120,
                    () => StartLevel((levelIndex + 1) % levels.Count));
            }
            else
            {
                Ui.Label(panel.transform, "Shelf is full!", 64, Ink, 0, 60, 800, 100);
                Ui.Label(panel.transform, "Revive puts 3 parcels back.\n(A rewarded ad will go here.)", 36, Ink, 0, 180, 800, 140);
                Ui.TextButton(panel.transform, "Revive", new Color(0.30f, 0.65f, 0.35f), 80, 420, 300, 120,
                    () => ApplyInstantly(state.Revive()));
                Ui.TextButton(panel.transform, "Retry", ButtonColor, 420, 420, 300, 120, () => StartLevel(levelIndex));
            }
        }
    }
}
