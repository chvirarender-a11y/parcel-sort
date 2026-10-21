using System;
using System.Collections;
using System.Collections.Generic;
using ParcelSort.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ParcelSort.Game.World
{
    /// <summary>Put on a tap collider in the garage so a raycast knows which lane or bay was hit.</summary>
    public class GarageSpot : MonoBehaviour
    {
        public Move Move;
    }

    /// <summary>
    /// The 3D farm seen from a bird's-eye camera: the crop field in the middle, a road loop around it,
    /// and the garage below with parking bays and lanes of harvesters.
    ///
    /// The rules run ahead of the picture: a tap is applied to the HarvestState at once and its
    /// LapResult is queued here, so players can tap several harvesters in a row while earlier laps
    /// are still driving.
    /// </summary>
    public class FarmBoard : MonoBehaviour
    {
        const float RoadGap = 0.95f;        // field edge to road center
        const float RoadWidth = 0.9f;
        const float BayGap = 2.0f;          // road center to bay row
        const float BaySpacing = 1.45f;
        const float LaneGap = 1.75f;        // bay row to lane fronts
        const float LaneSpacing = 1.9f;
        const float QueueSpacing = 1.4f;
        const int VisibleQueue = 3;
        const float DriveSpeed = 11f;
        const float HoldSeconds = 0.35f;
        const float CameraPitch = 62f;
        const float CameraFov = 38f;
        // Viewport band the board must fit in; the top is left for the HUD.
        const float ViewMinX = 0.03f, ViewMaxX = 0.97f, ViewMinY = 0.02f, ViewMaxY = 0.80f;

        static readonly Color Grass = new Color(0.62f, 0.83f, 0.47f);
        static readonly Color FieldEdge = new Color(0.48f, 0.33f, 0.22f);
        static readonly Color Road = new Color(0.86f, 0.80f, 0.68f);
        static readonly Color RoadLine = new Color(0.98f, 0.96f, 0.90f);
        static readonly Color Pad = new Color(0.80f, 0.86f, 0.88f);
        static readonly Color BayFree = new Color(0.97f, 0.97f, 0.95f);
        static readonly Color LanePad = new Color(0.70f, 0.78f, 0.82f);
        static readonly Color Highlight = new Color(1f, 0.95f, 0.55f);

        /// <summary>A short tap on a lane or bay.</summary>
        public event Action<Move> Tapped;
        /// <summary>A press held long enough to preview the route; released without dispatching.</summary>
        public event Action<Move> Held;
        /// <summary>Raised when every queued lap has finished animating.</summary>
        public event Action Settled;

        public bool Idle { get { return queue.Count == 0 && !running; } }

        Camera cam;
        FarmKit kit;
        Transform scenery;
        Transform pieces;
        Transform previewMarks;

        int width, height;
        float roadLeft, roadRight, roadBottom, roadTop, bayZ, laneZ;
        int laneCount;
        List<Station> stations;

        GameObject[,] cropViews;
        readonly List<List<Unit>> laneViews = new List<List<Unit>>();
        readonly List<Unit> bayViews = new List<Unit>();
        readonly List<GameObject> bayPads = new List<GameObject>();
        readonly List<Unit> waiting = new List<Unit>();
        readonly HashSet<int> pendingParks = new HashSet<int>();
        readonly Queue<KeyValuePair<LapResult, Unit>> queue = new Queue<KeyValuePair<LapResult, Unit>>();
        bool running;

        GarageSpot pressed;
        float pressTime;
        bool holding;

        /// <summary>A harvester on screen and its capacity badge.</summary>
        class Unit
        {
            public GameObject Go;
            public TextMesh Label;
            public int Capacity;

            public Transform T { get { return Go.transform; } }

            public void SetCapacity(int capacity)
            {
                Capacity = capacity;
                Label.text = capacity.ToString();
            }
        }

        void Awake()
        {
            kit = new FarmKit();
            SetUpCameraAndLight();
        }

        // ---- input ---------------------------------------------------------------------------

        void Update()
        {
            if (Input.GetMouseButtonDown(0) && !PointerOverUi())
            {
                pressed = SpotUnderPointer();
                pressTime = Time.time;
                holding = false;
            }
            if (pressed == null) return;

            if (!holding && Input.GetMouseButton(0) && Time.time - pressTime >= HoldSeconds)
            {
                holding = true;
                if (Held != null) Held(pressed.Move);
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (holding) ClearPreview();
                else if (Tapped != null) Tapped(pressed.Move);
                pressed = null;
                holding = false;
            }
        }

        GarageSpot SpotUnderPointer()
        {
            RaycastHit hit;
            if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit, 500f)) return null;
            return hit.collider.GetComponent<GarageSpot>();
        }

        static bool PointerOverUi()
        {
            var events = EventSystem.current;
            if (events == null) return false;
            if (Input.touchCount > 0) return events.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return events.IsPointerOverGameObject();
        }

        /// <summary>A parked harvester can be sent once it has visibly arrived in its bay.</summary>
        public bool CanTapBay(int bay)
        {
            return bay < bayViews.Count && bayViews[bay] != null && !pendingParks.Contains(bay);
        }

        // ---- building ------------------------------------------------------------------------

        /// <summary>Builds the farm for a new level, frames the camera and places the pieces.</summary>
        public void BuildLevel(HarvestState state)
        {
            StopAllCoroutines();
            queue.Clear();
            running = false;
            if (scenery != null) Destroy(scenery.gameObject);
            scenery = new GameObject("Scenery").transform;

            width = state.Width;
            height = state.Height;
            laneCount = state.LaneCount;
            stations = state.Stations;
            roadRight = width / 2f + RoadGap;
            roadLeft = -roadRight;
            roadTop = height / 2f + RoadGap;
            roadBottom = -roadTop;
            bayZ = roadBottom - BayGap;
            laneZ = bayZ - LaneGap;

            kit.Box("Grass", scenery, new Vector3(80, 0.2f, 80), 0.05f, Grass, new Vector3(0, -0.1f, 0));
            kit.Box("Field", scenery, new Vector3(width + 0.3f, 0.14f, height + 0.3f), 0.08f, FieldEdge, new Vector3(0, 0.0f, 0));
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    kit.Box("Soil", scenery, new Vector3(0.9f, 0.08f, 0.9f), 0.04f, FarmKit.Soil, CellPos(x, y) + new Vector3(0, 0.07f, 0));

            float roadW = roadRight - roadLeft, roadH = roadTop - roadBottom;
            kit.Box("Road", scenery, new Vector3(roadW + RoadWidth, 0.06f, RoadWidth), 0.03f, Road, new Vector3(0, 0.03f, roadBottom));
            kit.Box("Road", scenery, new Vector3(roadW + RoadWidth, 0.06f, RoadWidth), 0.03f, Road, new Vector3(0, 0.03f, roadTop));
            kit.Box("Road", scenery, new Vector3(RoadWidth, 0.06f, roadH + RoadWidth), 0.03f, Road, new Vector3(roadLeft, 0.03f, 0));
            kit.Box("Road", scenery, new Vector3(RoadWidth, 0.06f, roadH + RoadWidth), 0.03f, Road, new Vector3(roadRight, 0.03f, 0));
            for (float x = roadLeft + 0.6f; x < roadRight - 0.4f; x += 0.8f)
            {
                kit.Box("Dash", scenery, new Vector3(0.36f, 0.02f, 0.07f), 0.02f, RoadLine, new Vector3(x, 0.07f, roadBottom));
                kit.Box("Dash", scenery, new Vector3(0.36f, 0.02f, 0.07f), 0.02f, RoadLine, new Vector3(x, 0.07f, roadTop));
            }
            for (float z = roadBottom + 0.6f; z < roadTop - 0.4f; z += 0.8f)
            {
                kit.Box("Dash", scenery, new Vector3(0.07f, 0.02f, 0.36f), 0.02f, RoadLine, new Vector3(roadLeft, 0.07f, z));
                kit.Box("Dash", scenery, new Vector3(0.07f, 0.02f, 0.36f), 0.02f, RoadLine, new Vector3(roadRight, 0.07f, z));
            }

            // Room for one more bay, which a revive adds.
            float garageLeft = Mathf.Min(roadLeft + RoadWidth / 2 + 0.1f, LaneX(0) - 1f);
            float garageRight = Mathf.Max(BayPos(state.BayCount, state.BayCount + 1).x + 0.8f, LaneX(laneCount - 1) + 1f);
            float garageDepth = bayZ - (laneZ - QueueSpacing * (VisibleQueue - 1)) + 1.6f;
            kit.Box("Garage", scenery, new Vector3(garageRight - garageLeft, 0.08f, garageDepth), 0.2f, Pad,
                new Vector3((garageLeft + garageRight) / 2, 0.04f, bayZ + 0.8f - garageDepth / 2));
            kit.Box("Driveway", scenery, new Vector3(RoadWidth, 0.06f, roadBottom - bayZ + 0.6f), 0.03f, Road,
                new Vector3(roadLeft, 0.03f, (roadBottom + bayZ - 0.6f) / 2));

            for (int lane = 0; lane < laneCount; lane++)
            {
                float x = LaneX(lane);
                float length = QueueSpacing * (VisibleQueue - 1) + 1.3f;
                kit.Box("Lane", scenery, new Vector3(1.3f, 0.04f, length), 0.15f, LanePad,
                    new Vector3(x, 0.1f, laneZ - length / 2 + 0.65f));
                AddSpot(new Move(false, lane), new Vector3(x, 0.6f, laneZ - length / 2 + 0.65f), new Vector3(LaneSpacing * 0.95f, 1.6f, length + 0.4f));
            }

            // The barn above the field, with hedges and trees around, in the spirit of a cozy overworld map.
            kit.Barn(scenery, new Vector3(0, 0, BarnZ));
            float treeX = roadRight + 1.6f, treeZ = roadTop + 1.4f;
            kit.Tree(scenery, new Vector3(-treeX + 0.6f, 0, BarnZ + 0.3f), 1.1f);
            kit.Tree(scenery, new Vector3(treeX - 0.2f, 0, treeZ - 0.4f), 0.9f);
            kit.Tree(scenery, new Vector3(-treeX - 0.4f, 0, 0.5f), 0.8f);
            kit.Tree(scenery, new Vector3(treeX + 0.3f, 0, -1.2f), 1.0f);
            kit.Bush(scenery, new Vector3(-treeX + 0.2f, 0, -2.5f));
            kit.Bush(scenery, new Vector3(treeX - 0.2f, 0, 2.2f));
            kit.Bush(scenery, new Vector3(-2.6f, 0, roadTop + 1.1f));

            FitCamera(state.BayCount + 1);
            Refresh(state);
        }

        void AddSpot(Move move, Vector3 center, Vector3 size)
        {
            var go = new GameObject(move.ToString(), typeof(BoxCollider), typeof(GarageSpot));
            go.transform.SetParent(scenery, false);
            go.transform.localPosition = center;
            go.GetComponent<BoxCollider>().size = size;
            go.GetComponent<GarageSpot>().Move = move;
        }

        /// <summary>Rebuilds crops, bays and harvesters to match the state, without animation.</summary>
        public void Refresh(HarvestState state)
        {
            ClearPreview();
            if (pieces != null) Destroy(pieces.gameObject);
            pieces = new GameObject("Pieces").transform;
            waiting.Clear();
            pendingParks.Clear();

            cropViews = new GameObject[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int crop = state.CropAt(x, y);
                    if (crop != Crops.None) cropViews[x, y] = kit.Crop(crop, pieces, CellPos(x, y) + new Vector3(0, 0.11f, 0));
                }
            }

            // Bay pads live with the pieces because a revive can add one.
            bayPads.Clear();
            bayViews.Clear();
            for (int bay = 0; bay < state.BayCount; bay++)
            {
                var pad = kit.Box("Bay", pieces, new Vector3(1.25f, 0.05f, 1.45f), 0.18f, BayFree, BayPos(bay, state.BayCount) + new Vector3(0, 0.09f, 0));
                bayPads.Add(pad);
                var spot = new GameObject("bay " + bay, typeof(BoxCollider), typeof(GarageSpot));
                spot.transform.SetParent(pad.transform, false);
                spot.GetComponent<BoxCollider>().size = new Vector3(BaySpacing * 0.98f, 3f, 1.7f);
                spot.GetComponent<GarageSpot>().Move = new Move(true, bay);

                var h = state.Bay(bay);
                bayViews.Add(h.IsEmpty ? null : NewUnit(h, BayPos(bay, state.BayCount), 180f));
            }

            laneViews.Clear();
            for (int lane = 0; lane < state.LaneCount; lane++)
            {
                var views = new List<Unit>();
                var harvesters = state.LaneQueue(lane);
                for (int i = 0; i < harvesters.Count; i++) views.Add(NewUnit(harvesters[i], QueuePos(lane, i), 180f));
                laneViews.Add(views);
            }
        }

        Unit NewUnit(Harvester h, Vector3 position, float heading)
        {
            TextMesh label;
            var go = kit.Harvester(h.Color, h.Capacity, pieces, position, out label);
            go.transform.rotation = Quaternion.Euler(0, heading, 0);
            go.SetActive(position.z > laneZ - QueueSpacing * VisibleQueue + 0.1f);
            return new Unit { Go = go, Label = label, Capacity = h.Capacity };
        }

        // ---- layout --------------------------------------------------------------------------

        Vector3 CellPos(int x, int y)
        {
            return new Vector3(x - (width - 1) / 2f, 0, y - (height - 1) / 2f);
        }

        Vector3 StationPos(Station s)
        {
            switch (s.Side)
            {
                case Side.Bottom: return new Vector3(CellPos(s.Line, 0).x, 0, roadBottom);
                case Side.Top: return new Vector3(CellPos(s.Line, 0).x, 0, roadTop);
                case Side.Right: return new Vector3(roadRight, 0, CellPos(0, s.Line).z);
                default: return new Vector3(roadLeft, 0, CellPos(0, s.Line).z);
            }
        }

        float BarnZ { get { return roadTop + 2.3f; } }

        float LaneX(int lane) { return (lane - (laneCount - 1) / 2f) * LaneSpacing; }

        Vector3 QueuePos(int lane, int i) { return new Vector3(LaneX(lane), 0.1f, laneZ - i * QueueSpacing); }

        Vector3 BayPos(int bay, int bays)
        {
            // Centered under the field, but never on the driveway down the left side.
            float first = Mathf.Max(roadLeft + 1.4f, -(bays - 1) / 2f * BaySpacing);
            return new Vector3(first + bay * BaySpacing, 0.1f, bayZ);
        }

        /// <summary>Line between the road and the bays that harvesters drive along to park.</summary>
        float ParkingLane { get { return bayZ + 1.05f; } }

        /// <summary>Tapped harvesters wait their turn on the grass beside the driveway.</summary>
        Vector3 WaitPos(int i)
        {
            return new Vector3(roadLeft - 1.25f, 0.1f, roadBottom - 0.6f - Mathf.Min(i, 2) * 1.3f);
        }

        // ---- laps ----------------------------------------------------------------------------

        /// <summary>Takes the dispatched harvester out of its lane or bay and queues its lap.</summary>
        public void Launch(LapResult lap)
        {
            ClearPreview();
            Unit unit;
            if (lap.FromBay)
            {
                unit = bayViews[lap.Source];
                bayViews[lap.Source] = null;
            }
            else
            {
                var lane = laneViews[lap.Source];
                unit = lane[0];
                lane.RemoveAt(0);
                for (int i = 0; i < lane.Count; i++)
                {
                    lane[i].Go.SetActive(i < VisibleQueue);
                    StartCoroutine(Slide(lane[i].T, QueuePos(lap.Source, i), 0.2f));
                }
            }
            if (lap.ParkedBay >= 0) pendingParks.Add(lap.ParkedBay);

            waiting.Add(unit);
            StartCoroutine(Hop(unit.T));
            StartCoroutine(Slide(unit.T, WaitPos(waiting.Count - 1), 0.25f));
            queue.Enqueue(new KeyValuePair<LapResult, Unit>(lap, unit));
            if (!running) StartCoroutine(RunQueue());
        }

        IEnumerator RunQueue()
        {
            running = true;
            while (queue.Count > 0)
            {
                var next = queue.Dequeue();
                yield return DriveLap(next.Key, next.Value);
            }
            running = false;
            if (Settled != null) Settled();
        }

        IEnumerator DriveLap(LapResult lap, Unit unit)
        {
            waiting.Remove(unit);
            for (int i = 0; i < waiting.Count; i++) StartCoroutine(Slide(waiting[i].T, WaitPos(i), 0.2f));

            var corners = new[]
            {
                new Vector3(roadLeft, 0, roadBottom), new Vector3(roadRight, 0, roadBottom),
                new Vector3(roadRight, 0, roadTop), new Vector3(roadLeft, 0, roadTop),
            };
            yield return DriveTo(unit, corners[0]);
            int pick = 0;
            for (int s = 0; s < stations.Count; s++)
            {
                if (s == width) yield return DriveTo(unit, corners[1]);
                if (s == width + height) yield return DriveTo(unit, corners[2]);
                if (s == 2 * width + height) yield return DriveTo(unit, corners[3]);
                yield return DriveTo(unit, StationPos(stations[s]));

                if (pick < lap.Picks.Count && lap.Picks[pick].Station == s)
                {
                    var p = lap.Picks[pick++];
                    var crop = cropViews[p.X, p.Y];
                    cropViews[p.X, p.Y] = null;
                    if (crop != null) StartCoroutine(Harvest(crop.transform, unit.T));
                    unit.SetCapacity(unit.Capacity - 1);
                    StartCoroutine(Squash(unit.T));
                }
                if (lap.Filled && s == lap.EndStation)
                {
                    if (lap.Combo) StartCoroutine(Popup("Combo x" + lap.Picks.Count + "!", unit.T.position + Vector3.up * 2f));
                    yield return new WaitForSeconds(0.12f);
                    yield return Leave(unit);
                    yield break;
                }
            }
            yield return DriveTo(unit, corners[0]);
            if (lap.Combo) StartCoroutine(Popup("Combo x" + lap.Picks.Count + "!", unit.T.position + Vector3.up * 2f));

            if (lap.Overflow)
            {
                yield return DriveTo(unit, new Vector3(roadLeft, 0, ParkingLane));
                yield return Shake(unit.T);
                StartCoroutine(Popup("No free bay!", unit.T.position + Vector3.up * 2f));
                // Leave it on the driveway; a revive or undo rebuilds the garage.
                yield break;
            }

            int bay = lap.ParkedBay;
            // A revive can add a bay after this lap was queued; place it against the current pads.
            Vector3 target = bayPads[bay].transform.position;
            target.y = 0.1f;
            yield return DriveTo(unit, new Vector3(roadLeft, 0, ParkingLane));
            yield return DriveTo(unit, new Vector3(target.x, 0, ParkingLane));
            yield return DriveTo(unit, target);
            yield return Turn(unit.T, 180f);
            bayViews[bay] = unit;
            pendingParks.Remove(bay);
        }

        IEnumerator DriveTo(Unit unit, Vector3 target)
        {
            var t = unit.T;
            target.y = 0.1f;
            while ((t.position - target).sqrMagnitude > 0.0001f)
            {
                Vector3 dir = target - t.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.0001f)
                    t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(dir), 900f * Time.deltaTime);
                t.position = Vector3.MoveTowards(t.position, target, DriveSpeed * Time.deltaTime);
                yield return null;
            }
        }

        IEnumerator Turn(Transform t, float heading)
        {
            var target = Quaternion.Euler(0, heading, 0);
            while (Quaternion.Angle(t.rotation, target) > 0.5f)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, target, 900f * Time.deltaTime);
                yield return null;
            }
            t.rotation = target;
        }

        /// <summary>A full harvester hops and shrinks away, off to the barn.</summary>
        IEnumerator Leave(Unit unit)
        {
            var t = unit.T;
            Vector3 start = t.position;
            for (float k = 0; k < 1f; k += Time.deltaTime / 0.35f)
            {
                t.position = start + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f;
                t.localScale = Vector3.one * (1f - k * k);
                yield return null;
            }
            Destroy(unit.Go);
        }

        IEnumerator Harvest(Transform crop, Transform harvester)
        {
            Vector3 start = crop.position;
            for (float k = 0; k < 1f; k += Time.deltaTime / 0.28f)
            {
                if (harvester == null) break;
                Vector3 target = harvester.position + Vector3.up * 0.9f;
                crop.position = Vector3.Lerp(start, target, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.2f;
                crop.localScale = Vector3.one * Mathf.Lerp(1f, 0.35f, k);
                yield return null;
            }
            Destroy(crop.gameObject);
        }

        static IEnumerator Squash(Transform t)
        {
            for (float k = 0; k < 1f; k += Time.deltaTime / 0.15f)
            {
                if (t == null) yield break;
                float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.12f;
                if (t.localScale.x > 0.99f) t.localScale = new Vector3(s, 2f - s, s);
                yield return null;
            }
            if (t != null && t.localScale.x > 0.99f) t.localScale = Vector3.one;
        }

        static IEnumerator Hop(Transform t)
        {
            for (float k = 0; k < 1f; k += Time.deltaTime / 0.2f)
            {
                if (t == null) yield break;
                float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.15f;
                t.localScale = new Vector3(2f - s, s, 2f - s);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        static IEnumerator Shake(Transform t)
        {
            Vector3 home = t.position;
            for (float k = 0; k < 1f; k += Time.deltaTime / 0.4f)
            {
                t.position = home + Vector3.right * Mathf.Sin(k * 40f) * 0.12f * (1f - k);
                yield return null;
            }
            t.position = home;
        }

        static IEnumerator Slide(Transform t, Vector3 target, float duration)
        {
            Vector3 start = t.position;
            for (float k = 0; k < 1f; k += Time.deltaTime / duration)
            {
                if (t == null) yield break;
                t.position = Vector3.Lerp(start, target, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            if (t != null) t.position = target;
        }

        IEnumerator Popup(string text, Vector3 position)
        {
            var go = new GameObject("Popup", typeof(MeshRenderer), typeof(TextMesh), typeof(Billboard));
            go.transform.position = position;
            var label = go.GetComponent<TextMesh>();
            label.font = Ui.Font;
            label.fontSize = 72;
            label.characterSize = 0.08f;
            label.anchor = TextAnchor.MiddleCenter;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(1f, 0.55f, 0.2f);
            label.text = text;
            go.GetComponent<MeshRenderer>().sharedMaterial = Ui.Font.material;
            for (float k = 0; k < 1f; k += Time.deltaTime / 0.9f)
            {
                go.transform.position = position + Vector3.up * k * 1.2f;
                go.transform.localScale = Vector3.one * Mathf.Min(1f, k * 6f);
                yield return null;
            }
            Destroy(go);
        }

        // ---- preview -------------------------------------------------------------------------

        /// <summary>Marks the crops a dispatch would pick, and where the harvester would stop.</summary>
        public void ShowPreview(LapResult lap)
        {
            ClearPreview();
            if (lap == null) return;
            previewMarks = new GameObject("Preview").transform;
            foreach (var p in lap.Picks)
            {
                kit.Prop("Mark", previewMarks, MeshFactory.Cylinder(0.46f, 0.04f), Highlight, CellPos(p.X, p.Y) + new Vector3(0, 0.13f, 0),
                    Quaternion.Euler(-90, 0, 0));
                var crop = cropViews[p.X, p.Y];
                if (crop != null) crop.transform.localScale = Vector3.one * 1.18f;
            }
            string text = lap.Picks.Count + " crops";
            if (lap.Filled) text += " - full!";
            else if (lap.Overflow) text += " - no bay left!";
            else text += " - parks";
            var label = new GameObject("Preview label", typeof(MeshRenderer), typeof(TextMesh), typeof(Billboard));
            label.transform.SetParent(previewMarks, false);
            label.transform.position = new Vector3(0, 1.2f, roadBottom - 0.2f);
            var tm = label.GetComponent<TextMesh>();
            tm.font = Ui.Font;
            tm.fontSize = 64;
            tm.characterSize = 0.07f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;
            tm.color = FarmKit.Ink;
            tm.text = text;
            label.GetComponent<MeshRenderer>().sharedMaterial = Ui.Font.material;
        }

        public void ClearPreview()
        {
            if (previewMarks == null) return;
            Destroy(previewMarks.gameObject);
            previewMarks = null;
            if (cropViews == null) return;
            foreach (var crop in cropViews) if (crop != null) crop.transform.localScale = Vector3.one;
        }

        // ---- camera --------------------------------------------------------------------------

        void SetUpCameraAndLight()
        {
            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Grass;
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
            sun.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
            sun.color = new Color(1f, 0.97f, 0.90f);
            sun.intensity = 1.0f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.4f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.68f);
        }

        /// <summary>Moves the camera back until field, road and garage fit, then centers it vertically.</summary>
        void FitCamera(int maxBays)
        {
            float left = WaitPos(0).x - 0.7f;
            float right = Mathf.Max(roadRight + RoadWidth / 2, BayPos(maxBays - 1, maxBays).x + 0.8f);
            float garageEnd = laneZ - QueueSpacing * (VisibleQueue - 1) - 0.8f;
            var points = new List<Vector3>
            {
                new Vector3(left, 0, roadTop + 0.5f), new Vector3(right, 0, roadTop + 0.5f),
                new Vector3(left, 1.9f, roadTop), new Vector3(right, 1.9f, roadTop),
                new Vector3(0, 3.4f, BarnZ),
                new Vector3(left, 0, garageEnd), new Vector3(right, 0, garageEnd),
            };

            var center = new Vector3((left + right) / 2, 0, (roadTop + garageEnd) / 2);
            Vector3 forward = cam.transform.forward;
            for (int pass = 0; pass < 2; pass++)
            {
                for (float dist = 8f; dist < 150f; dist += 0.25f)
                {
                    cam.transform.position = center - forward * dist;
                    if (AllInView(points)) break;
                }
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
