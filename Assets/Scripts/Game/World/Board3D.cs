using System;
using System.Collections;
using System.Collections.Generic;
using ParcelSort.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ParcelSort.Game.World
{
    /// <summary>Put on a stack's tap collider so a raycast knows which stack was hit.</summary>
    public class StackHit : MonoBehaviour
    {
        public int Index;
    }

    /// <summary>
    /// The 3D board seen from a bird's-eye camera: parcel towers in front, the buffer shelf
    /// behind them and the loading dock with trucks at the back. Everything is built from
    /// code with rounded boxes, so no prefabs are needed; real models can replace the
    /// placeholder truck later.
    /// </summary>
    public class Board3D : MonoBehaviour
    {
        const float ParcelSize = 0.9f;
        const float TruckParcelScale = 0.8f;
        const float Step = 0.94f;
        const float TowerSpacing = 1.25f;
        const float SlotSpacing = 1.2f;
        const float PalletHeight = 0.16f;
        const float ShelfHeight = 0.3f;
        const float DockSpacing = 4.8f;
        const float CameraPitch = 60f;
        const float CameraFov = 40f;
        // Viewport band the board must fit in; the top is left for the HUD.
        const float ViewMinX = 0.03f, ViewMaxX = 0.97f, ViewMinY = 0.03f, ViewMaxY = 0.84f;

        public static readonly Color[] Palette =
        {
            new Color(0.93f, 0.30f, 0.30f), // red
            new Color(0.35f, 0.78f, 0.40f), // green
            new Color(0.28f, 0.55f, 0.95f), // blue
            new Color(0.99f, 0.82f, 0.25f), // yellow
            new Color(0.65f, 0.40f, 0.88f), // purple
            new Color(0.99f, 0.58f, 0.20f), // orange
            new Color(0.30f, 0.85f, 0.90f), // cyan
            new Color(0.96f, 0.96f, 0.96f), // white
        };

        static readonly Color FloorColor = new Color(0.93f, 0.89f, 0.81f);
        static readonly Color DockColor = new Color(0.62f, 0.62f, 0.66f);
        static readonly Color WoodColor = new Color(0.78f, 0.58f, 0.38f);
        static readonly Color ShelfColor = new Color(0.86f, 0.70f, 0.50f);
        static readonly Color SlotColor = new Color(0.70f, 0.54f, 0.36f);
        static readonly Color HiddenColor = new Color(0.36f, 0.34f, 0.33f);
        static readonly Color ChassisColor = new Color(0.30f, 0.30f, 0.33f);
        static readonly Color CabColor = new Color(0.97f, 0.95f, 0.90f);
        static readonly Color GlassColor = new Color(0.45f, 0.62f, 0.80f);
        static readonly Color HubColor = new Color(0.85f, 0.85f, 0.88f);

        public event Action<int> StackTapped;
        public bool Busy { get; private set; }

        Camera cam;
        Material baseMaterial;
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

        Transform scenery;
        Transform pieces;
        int stackCount;
        int dockCount;
        float shelfZ;
        float dockZ;
        readonly List<List<GameObject>> towers = new List<List<GameObject>>();
        GameObject[] trucks = new GameObject[0];

        void Awake()
        {
            baseMaterial = Resources.Load<Material>("Materials/Toy");
            if (baseMaterial == null) baseMaterial = new Material(Shader.Find("Standard"));
            SetUpCameraAndLight();
        }

        void Update()
        {
            if (Busy || !Input.GetMouseButtonDown(0)) return;
            if (PointerOverUi()) return;
            RaycastHit hit;
            if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit, 500f)) return;
            var stack = hit.collider.GetComponent<StackHit>();
            if (stack != null && StackTapped != null) StackTapped(stack.Index);
        }

        static bool PointerOverUi()
        {
            var events = EventSystem.current;
            if (events == null) return false;
            if (Input.touchCount > 0) return events.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return events.IsPointerOverGameObject();
        }

        /// <summary>Builds the scenery for a new level, frames the camera and places the pieces.</summary>
        public void BuildLevel(GameState state)
        {
            StopAllCoroutines();
            Busy = false;
            if (scenery != null) Destroy(scenery.gameObject);
            scenery = new GameObject("Scenery").transform;

            stackCount = state.Stacks.Count;
            dockCount = state.Docks.Length;
            int tallest = 1;
            foreach (var stack in state.Stacks) tallest = Mathf.Max(tallest, stack.Count);
            float towerHeight = PalletHeight + tallest * Step;

            // Keep the shelf far enough back that the tallest tower never hides it from the camera.
            shelfZ = Mathf.Max(2.6f, towerHeight / Mathf.Tan(CameraPitch * Mathf.Deg2Rad) + 1.6f);
            dockZ = shelfZ + 3.0f;

            Prop("Floor", scenery, MeshFactory.RoundedBox(new Vector3(40, 0.2f, 60), 0.05f, 1), FloorColor, new Vector3(0, -0.1f, 0));
            Prop("Dock", scenery, MeshFactory.RoundedBox(new Vector3(dockCount * DockSpacing + 1.2f, 0.06f, 3.2f), 0.03f, 1),
                DockColor, new Vector3(0, 0.03f, dockZ));
            for (int d = 0; d < dockCount; d++)
            {
                // Painted parking bay lines.
                float x = DockX(d);
                foreach (float side in new[] { -1f, 1f })
                {
                    Prop("Bay line", scenery, MeshFactory.RoundedBox(new Vector3(4.4f, 0.02f, 0.08f), 0.01f, 1), Color.white,
                        new Vector3(x, 0.07f, dockZ + side * 1.15f));
                }
            }

            for (int s = 0; s < stackCount; s++)
            {
                var pallet = Prop("Pallet " + s, scenery, MeshFactory.RoundedBox(new Vector3(1.08f, PalletHeight, 1.08f), 0.04f, 2),
                    WoodColor, new Vector3(TowerX(s), PalletHeight / 2, 0));
                var hit = new GameObject("Tap area", typeof(BoxCollider), typeof(StackHit));
                hit.transform.SetParent(pallet.transform, false);
                var box = hit.GetComponent<BoxCollider>();
                box.center = new Vector3(0, towerHeight / 2, 0);
                box.size = new Vector3(TowerSpacing * 0.98f, towerHeight + 0.6f, 1.6f);
                hit.GetComponent<StackHit>().Index = s;
            }

            FitCamera(towerHeight, state.BufferCapacity + 1);
            Refresh(state);
        }

        /// <summary>Rebuilds parcels, shelf and trucks to match the state, without animation.</summary>
        public void Refresh(GameState state)
        {
            if (pieces != null) Destroy(pieces.gameObject);
            pieces = new GameObject("Pieces").transform;

            towers.Clear();
            for (int s = 0; s < state.Stacks.Count; s++)
            {
                var tower = new List<GameObject>();
                var stack = state.Stacks[s];
                for (int i = 0; i < stack.Count; i++)
                {
                    tower.Add(Parcel(pieces, stack[i].Hidden ? HiddenColor : Palette[stack[i].Color], TowerPos(s, i)));
                }
                towers.Add(tower);
            }

            int cap = state.BufferCapacity;
            Prop("Shelf", pieces, MeshFactory.RoundedBox(new Vector3(cap * SlotSpacing + 0.3f, ShelfHeight, 1.35f), 0.08f, 3),
                ShelfColor, new Vector3(0, ShelfHeight / 2, shelfZ));
            for (int i = 0; i < cap; i++)
            {
                Prop("Slot", pieces, MeshFactory.RoundedBox(new Vector3(1.0f, 0.02f, 1.0f), 0.01f, 1), SlotColor,
                    new Vector3(ShelfPos(i, cap).x, ShelfHeight + 0.005f, shelfZ));
            }
            for (int i = 0; i < state.Buffer.Count; i++) Parcel(pieces, Palette[state.Buffer[i].Color], ShelfPos(i, cap));

            trucks = new GameObject[state.Docks.Length];
            for (int d = 0; d < state.Docks.Length; d++)
            {
                var dock = state.Docks[d];
                if (dock.IsEmpty) continue;
                trucks[d] = Truck(dock.Color, dock.Load, state.TruckCapacity, DockPos(d));
            }
        }

        /// <summary>Animates one tap, then refreshes to the new state.</summary>
        public IEnumerator PlayMove(GameState before, MoveResult move, GameState after)
        {
            Busy = true;
            var tower = towers[move.Stack];
            var parcel = tower[tower.Count - 1];
            tower.RemoveAt(tower.Count - 1);

            if (move.ToDock >= 0 && trucks[move.ToDock] != null)
            {
                var truck = trucks[move.ToDock].transform;
                int slot = before.Docks[move.ToDock].Load;
                Vector3 target = truck.TransformPoint(TruckSlotLocal(slot, before.TruckCapacity));
                yield return Fly(parcel.transform, target, TruckParcelScale);
                parcel.transform.SetParent(truck, true);
            }
            else
            {
                yield return Fly(parcel.transform, ShelfPos(before.Buffer.Count, before.BufferCapacity), 1f);
            }

            var departed = new HashSet<int>(move.Departures);
            if (departed.Count > 0)
            {
                foreach (int d in departed)
                {
                    if (d < trucks.Length && trucks[d] != null) StartCoroutine(Drive(trucks[d].transform, 0f, 14f, 0.5f, true));
                }
                yield return new WaitForSeconds(0.5f);
            }

            Refresh(after);

            bool arriving = false;
            foreach (int d in departed)
            {
                if (d < trucks.Length && trucks[d] != null)
                {
                    StartCoroutine(Drive(trucks[d].transform, -14f, 0f, 0.4f, false));
                    arriving = true;
                }
            }
            if (arriving) yield return new WaitForSeconds(0.4f);
            Busy = false;
        }

        // ---- layout ----

        float TowerX(int s) { return (s - (stackCount - 1) / 2f) * TowerSpacing; }
        float DockX(int d) { return (d - (dockCount - 1) / 2f) * DockSpacing; }

        Vector3 TowerPos(int s, int i)
        {
            return new Vector3(TowerX(s), PalletHeight + ParcelSize / 2 + i * Step, 0);
        }

        Vector3 ShelfPos(int i, int capacity)
        {
            return new Vector3((i - (capacity - 1) / 2f) * SlotSpacing, ShelfHeight + ParcelSize / 2, shelfZ);
        }

        Vector3 DockPos(int d) { return new Vector3(DockX(d), 0, dockZ); }

        static Vector3 TruckSlotLocal(int slot, int capacity)
        {
            float bedStart = -1.95f, bedLength = 2.75f;
            float x = bedStart + bedLength * (slot + 0.5f) / capacity;
            return new Vector3(x, 0.85f + ParcelSize * TruckParcelScale / 2, 0);
        }

        // ---- builders ----

        GameObject Parcel(Transform parent, Color color, Vector3 position)
        {
            var parcel = Prop("Parcel", parent, MeshFactory.RoundedBox(Vector3.one * ParcelSize, 0.14f, 5), color, position);
            // Packing tape across the top, slightly darker than the box.
            Prop("Tape", parcel.transform, MeshFactory.RoundedBox(new Vector3(0.22f, 0.02f, ParcelSize * 1.01f), 0.01f, 1),
                Color.Lerp(color, Color.black, 0.18f), new Vector3(0, ParcelSize / 2, 0));
            return parcel;
        }

        /// <summary>Placeholder truck facing +X: cab at the front, an open cargo bed behind it.</summary>
        GameObject Truck(int color, int load, int capacity, Vector3 position)
        {
            var root = new GameObject("Truck").transform;
            root.SetParent(pieces, false);
            root.localPosition = position;
            Color body = Palette[color];

            // Chunky toy proportions: oversized wheels, a short rounded cab, a thick bumper.
            Prop("Chassis", root, MeshFactory.RoundedBox(new Vector3(4.2f, 0.3f, 1.4f), 0.12f, 3), ChassisColor, new Vector3(-0.1f, 0.58f, 0));
            foreach (float x in new[] { -1.3f, 1.3f })
            {
                foreach (float z in new[] { -0.72f, 0.72f })
                {
                    var wheel = Prop("Wheel", root, MeshFactory.Cylinder(0.44f, 0.36f), ChassisColor, new Vector3(x, 0.44f, z));
                    Prop("Hub", wheel.transform, MeshFactory.Cylinder(0.2f, 0.38f), HubColor, Vector3.zero);
                }
            }

            Prop("Cargo floor", root, MeshFactory.RoundedBox(new Vector3(3.0f, 0.14f, 1.7f), 0.06f, 2), body, new Vector3(-0.575f, 0.78f, 0));
            Prop("Cargo back", root, MeshFactory.RoundedBox(new Vector3(0.16f, 0.5f, 1.7f), 0.07f, 3), body, new Vector3(-2.0f, 1.0f, 0));
            Prop("Cargo front", root, MeshFactory.RoundedBox(new Vector3(0.16f, 0.5f, 1.7f), 0.07f, 3), body, new Vector3(0.85f, 1.0f, 0));
            foreach (float z in new[] { -0.79f, 0.79f })
            {
                Prop("Cargo side", root, MeshFactory.RoundedBox(new Vector3(3.0f, 0.5f, 0.14f), 0.06f, 3), body, new Vector3(-0.575f, 1.0f, z));
            }

            Prop("Cab", root, MeshFactory.RoundedBox(new Vector3(1.25f, 1.25f, 1.65f), 0.36f, 5), body, new Vector3(1.55f, 1.3f, 0));
            Prop("Cab roof", root, MeshFactory.RoundedBox(new Vector3(1.0f, 0.2f, 1.45f), 0.09f, 3), CabColor, new Vector3(1.45f, 1.98f, 0));
            Prop("Windshield", root, MeshFactory.RoundedBox(new Vector3(0.08f, 0.5f, 1.3f), 0.06f, 2), GlassColor, new Vector3(2.16f, 1.5f, 0));
            Prop("Bumper", root, MeshFactory.RoundedBox(new Vector3(0.26f, 0.3f, 1.7f), 0.12f, 3), CabColor, new Vector3(2.25f, 0.72f, 0));

            for (int i = 0; i < load; i++)
            {
                var parcel = Parcel(root, body, Vector3.zero);
                parcel.transform.localPosition = TruckSlotLocal(i, capacity);
                parcel.transform.localScale = Vector3.one * TruckParcelScale;
            }
            return root.gameObject;
        }

        GameObject Prop(string name, Transform parent, Mesh mesh, Color color, Vector3 localPosition)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(color);
            return go;
        }

        Material MaterialFor(Color color)
        {
            Material material;
            if (!materials.TryGetValue(color, out material))
            {
                material = new Material(baseMaterial) { color = color };
                materials[color] = material;
            }
            return material;
        }

        // ---- animation ----

        static IEnumerator Fly(Transform item, Vector3 target, float endScale, float duration = 0.32f)
        {
            Vector3 start = item.position;
            Vector3 startScale = item.localScale;
            float arc = 1.2f + Vector3.Distance(start, target) * 0.15f;
            for (float t = 0; t < 1f; t += Time.deltaTime / duration)
            {
                float e = 1f - (1f - t) * (1f - t);
                item.position = Vector3.Lerp(start, target, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * arc;
                item.localScale = Vector3.Lerp(startScale, Vector3.one * endScale, e);
                yield return null;
            }
            item.position = target;
            item.localScale = Vector3.one * endScale;
        }

        static IEnumerator Drive(Transform truck, float fromOffset, float toOffset, float duration, bool accelerate)
        {
            Vector3 home = truck.position;
            truck.position = home + Vector3.right * fromOffset;
            for (float t = 0; t < 1f; t += Time.deltaTime / duration)
            {
                if (truck == null) yield break;
                float e = accelerate ? t * t : 1f - (1f - t) * (1f - t);
                truck.position = home + Vector3.right * Mathf.Lerp(fromOffset, toOffset, e);
                yield return null;
            }
            if (truck != null) truck.position = home + Vector3.right * toOffset;
        }

        // ---- camera ----

        void SetUpCameraAndLight()
        {
            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FloorColor;
            cam.fieldOfView = CameraFov;
            cam.transform.rotation = Quaternion.Euler(CameraPitch, 0, 0);

            Light sun = null;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional) sun = light;
            }
            if (sun == null)
            {
                sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.45f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.58f, 0.62f);
        }

        /// <summary>Moves the camera back until the whole board fits, then centers it vertically.</summary>
        void FitCamera(float towerHeight, int maxShelfSlots)
        {
            float towersHalf = Mathf.Max(stackCount * TowerSpacing, maxShelfSlots * SlotSpacing) / 2 + 0.3f;
            float docksHalf = dockCount * DockSpacing / 2;
            var points = new List<Vector3>
            {
                new Vector3(-towersHalf, 0, -0.8f), new Vector3(towersHalf, 0, -0.8f),
                new Vector3(-towersHalf, towerHeight, 0), new Vector3(towersHalf, towerHeight, 0),
                new Vector3(-docksHalf, 0, dockZ + 1.6f), new Vector3(docksHalf, 0, dockZ + 1.6f),
                new Vector3(-docksHalf, 2.2f, dockZ + 1.2f), new Vector3(docksHalf, 2.2f, dockZ + 1.2f),
            };

            var center = new Vector3(0, 0.5f, dockZ * 0.45f);
            Vector3 forward = cam.transform.forward;
            for (int pass = 0; pass < 2; pass++)
            {
                for (float dist = 8f; dist < 150f; dist += 0.25f)
                {
                    cam.transform.position = center - forward * dist;
                    if (AllInView(points)) break;
                }
                // Center the board in the free band by sliding the look-at point along the floor.
                float minY = 1f, maxY = 0f;
                foreach (var p in points)
                {
                    float y = cam.WorldToViewportPoint(p).y;
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
                float shift = ((ViewMinY + ViewMaxY) - (minY + maxY)) / 2f;
                float viewHeight = 2f * Vector3.Distance(cam.transform.position, center) * Mathf.Tan(CameraFov * Mathf.Deg2Rad / 2);
                center -= new Vector3(0, 0, shift * viewHeight / Mathf.Sin(CameraPitch * Mathf.Deg2Rad));
            }
        }

        bool AllInView(List<Vector3> points)
        {
            foreach (var p in points)
            {
                Vector3 v = cam.WorldToViewportPoint(p);
                if (v.z <= 0 || v.x < ViewMinX || v.x > ViewMaxX || v.y < ViewMinY || v.y > ViewMaxY) return false;
            }
            return true;
        }
    }
}
