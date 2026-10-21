using System.Collections.Generic;
using ParcelSort.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ParcelSort.Game
{
    /// <summary>
    /// Prototype game screen: loads levels from Resources/Levels, draws the board with plain
    /// colored boxes and redraws everything after each tap. Art and animation come later.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        const string ProgressKey = "ParcelSort.Level";
        const float Width = 1080;

        static readonly Color Background = new Color(0.95f, 0.93f, 0.88f);
        static readonly Color Panel = new Color(0.82f, 0.78f, 0.70f);
        static readonly Color Slot = new Color(0.70f, 0.66f, 0.58f);
        static readonly Color Ink = new Color(0.20f, 0.18f, 0.16f);
        static readonly Color ButtonColor = new Color(0.25f, 0.45f, 0.75f);
        static readonly Color HiddenColor = new Color(0.35f, 0.33f, 0.30f);

        static readonly Color[] ParcelPalette =
        {
            new Color(0.90f, 0.25f, 0.25f), // red
            new Color(0.30f, 0.75f, 0.35f), // green
            new Color(0.25f, 0.50f, 0.95f), // blue
            new Color(0.98f, 0.82f, 0.20f), // yellow
            new Color(0.62f, 0.35f, 0.85f), // purple
            new Color(0.98f, 0.55f, 0.15f), // orange
            new Color(0.25f, 0.85f, 0.90f), // cyan
            new Color(0.97f, 0.97f, 0.97f), // white
        };

        // Second channel besides color, for color-blind players.
        static readonly string[] ParcelSymbols = { "R", "G", "B", "Y", "P", "O", "C", "W" };

        readonly List<LevelData> levels = new List<LevelData>();
        int levelIndex;
        GameState state;
        RectTransform root;

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

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvas = Ui.CreateCanvas("Canvas");
            root = Ui.Fill("Root", canvas.transform);

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
            Redraw();
        }

        void OnTapStack(int stack)
        {
            if (state.Tap(stack) == null) return;
            if (state.Status == GameStatus.Won)
            {
                PlayerPrefs.SetInt(ProgressKey, (levelIndex + 1) % levels.Count);
                PlayerPrefs.Save();
            }
            Redraw();
        }

        void Redraw()
        {
            Ui.Clear(root);
            DrawTopBar();
            DrawDocks();
            DrawBuffer();
            DrawStacks();
            if (state.Status != GameStatus.Playing) DrawResult();
        }

        void DrawTopBar()
        {
            Ui.Label(root, "Level " + state.Level.id, 56, Ink, 40, 50, 460, 100, TextAnchor.MiddleLeft);
            Ui.TextButton(root, "Restart", ButtonColor, 520, 50, 170, 100, () => StartLevel(levelIndex));
            var undo = Ui.TextButton(root, "Undo", ButtonColor, 705, 50, 150, 100, () => { if (state.Undo()) Redraw(); });
            undo.interactable = state.CanUndo;
            var extra = Ui.TextButton(root, "+1 Slot", ButtonColor, 870, 50, 170, 100, () => { if (state.AddBufferSlot()) Redraw(); });
            extra.interactable = !state.ExtraSlotUsed && state.Status == GameStatus.Playing;
        }

        void DrawDocks()
        {
            const float truckW = 460, truckH = 250, top = 190;
            for (int d = 0; d < state.Docks.Length && d < 2; d++)
            {
                float x = 60 + d * 500;
                var dock = state.Docks[d];
                if (dock.IsEmpty)
                {
                    var empty = Ui.Box(root, Panel, x, top, truckW, truckH, "Empty dock");
                    Ui.Label(empty.transform, "(empty)", 40, Ink, 0, 0, truckW, truckH);
                    continue;
                }
                if (Art.TruckCab != null && Art.TruckCargo != null)
                {
                    DrawTruck(dock, x, top, truckW, truckH);
                    continue;
                }
                var body = Ui.Box(root, ParcelPalette[dock.Color], x, top, truckW, truckH, "Truck");
                Ui.Label(body.transform, "TRUCK  " + ParcelSymbols[dock.Color], 40, Ink, 0, 10, truckW, 60);
                for (int s = 0; s < state.TruckCapacity; s++)
                {
                    float sx = 25 + s * (truckW - 50) / state.TruckCapacity;
                    float sw = (truckW - 50) / state.TruckCapacity - 15;
                    Ui.Box(body.transform, s < dock.Load ? Ink : new Color(1, 1, 1, 0.6f), sx, 90, sw, 135, "Slot");
                }
            }

            Ui.Label(root, "Next:", 40, Ink, 60, 460, 140, 70, TextAnchor.MiddleLeft);
            int shown = 0;
            for (int t = state.NextTruck; t < state.Level.trucks.Length && shown < 6; t++, shown++)
            {
                int color = ParcelColors.FromLetter(state.Level.trucks[t]);
                var next = Ui.Box(root, ParcelPalette[color], 200 + shown * 90, 465, 70, 60, "Next truck");
                Ui.Label(next.transform, ParcelSymbols[color], 32, Ink, 0, 0, 70, 60);
            }
            if (shown == 0) Ui.Label(root, "none", 40, Ink, 200, 460, 200, 70, TextAnchor.MiddleLeft);
        }

        /// <summary>Truck drawn from art: cab, then loaded parcels, then the tinted cargo box in front.</summary>
        void DrawTruck(Dock dock, float x, float top, float w, float h)
        {
            float cabH = w / Art.Aspect(Art.TruckCab);
            float cabY = top + h - cabH;
            var cab = Ui.Box(root, Color.white, x, cabY, w, cabH, "Truck cab");
            cab.sprite = Art.TruckCab;

            float cargoW = w * Art.CargoWidth;
            float cargoH = cargoW / Art.Aspect(Art.TruckCargo);
            float cargoX = x + w * Art.CargoLeft;
            float cargoY = cabY + cabH * Art.BedTop - cargoH;

            float parcelSize = cargoW / state.TruckCapacity * 0.62f;
            for (int i = 0; i < dock.Load; i++)
            {
                float px = cargoX + cargoW * (i + 0.5f) / state.TruckCapacity - parcelSize / 2;
                DrawParcel(root, dock.Color, false, px, cargoY + cargoH * 0.55f - parcelSize, parcelSize);
            }

            var cargo = Ui.Box(root, ParcelPalette[dock.Color], cargoX, cargoY, cargoW, cargoH, "Truck cargo");
            cargo.sprite = Art.TruckCargo;
        }

        void DrawBuffer()
        {
            const float size = 130, gap = 16, top = 580;
            Ui.Label(root, "Shelf " + state.Buffer.Count + "/" + state.BufferCapacity, 40, Ink, 60, top - 10, 400, 50, TextAnchor.MiddleLeft);
            float total = state.BufferCapacity * size + (state.BufferCapacity - 1) * gap;
            float x0 = (Width - total) / 2;
            for (int i = 0; i < state.BufferCapacity; i++)
            {
                float x = x0 + i * (size + gap);
                if (i < state.Buffer.Count) DrawParcel(root, state.Buffer[i].Color, false, x, top + 50, size);
                else Ui.Box(root, Slot, x, top + 50, size, size, "Empty slot");
            }
        }

        void DrawStacks()
        {
            const float top = 800, bottom = 1880;
            int n = state.Stacks.Count;
            float colW = Mathf.Min(150, (Width - 40) / n);
            float x0 = (Width - colW * n) / 2;
            int tallest = 1;
            foreach (var stack in state.Stacks) tallest = Mathf.Max(tallest, stack.Count);
            float size = Mathf.Min(colW - 16, (bottom - top) / tallest - 8);
            // Art parcels are drawn at a slight angle, so they overlap a little to look stacked.
            float step = Art.ParcelBase != null ? size * 0.9f : size + 8;

            for (int s = 0; s < n; s++)
            {
                int stackIndex = s;
                var column = Ui.Box(root, new Color(0, 0, 0, 0.06f), x0 + s * colW + 4, top, colW - 8, bottom - top, "Stack");
                var button = column.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.onClick.AddListener(() => OnTapStack(stackIndex));

                var stack = state.Stacks[s];
                for (int i = 0; i < stack.Count; i++)
                {
                    float y = (bottom - top) - size - i * step - 8;
                    DrawParcel(column.transform, stack[i].Color, stack[i].Hidden, (colW - 8 - size) / 2, y, size);
                }
            }
        }

        void DrawParcel(Transform parent, int color, bool hidden, float x, float y, float size)
        {
            var box = Ui.Box(parent, hidden ? HiddenColor : ParcelPalette[color], x, y, size, size, "Parcel");
            box.raycastTarget = false;
            if (Art.ParcelBase != null)
            {
                box.preserveAspect = true;
                if (hidden && Art.ParcelHidden != null)
                {
                    box.sprite = Art.ParcelHidden;
                    box.color = Color.white;
                    return;
                }
                box.sprite = Art.ParcelBase;
            }
            if (Art.ParcelBase != null)
            {
                // Small mark on the box's front face, until the symbol art arrives.
                Ui.Label(box.transform, hidden ? "?" : ParcelSymbols[color], (int)(size * 0.28f),
                    new Color(1, 1, 1, 0.85f), 0, size * 0.12f, size, size);
                return;
            }
            Ui.Label(box.transform, hidden ? "?" : ParcelSymbols[color], (int)(size * 0.4f), hidden ? Color.white : Ink, 0, 0, size, size);
        }

        void DrawResult()
        {
            Ui.FillBox(root, new Color(0, 0, 0, 0.55f), "Shade");
            var panel = Ui.Box(root, Background, 140, 600, 800, 640, "Result");

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
                    () => { if (state.Revive()) Redraw(); });
                Ui.TextButton(panel.transform, "Retry", ButtonColor, 420, 420, 300, 120, () => StartLevel(levelIndex));
            }
        }
    }
}
