using System;
using System.Collections.Generic;
using System.Text;

namespace ParcelSort.Core
{
    /// <summary>A harvester: the crop color it picks and how many crops it still has room for.</summary>
    public struct Harvester
    {
        public int Color;
        public int Capacity;

        public Harvester(int color, int capacity)
        {
            Color = color;
            Capacity = capacity;
        }

        public bool IsEmpty { get { return Capacity <= 0; } }

        public override string ToString()
        {
            return IsEmpty ? "-" : Crops.ToLetter(Color) + Capacity.ToString();
        }
    }

    /// <summary>The four sides of the road loop, in driving order (counter-clockwise seen from above).</summary>
    public enum Side { Bottom, Right, Top, Left }

    /// <summary>
    /// A stop on the road loop. A harvester at a station looks across one column (Bottom/Top)
    /// or one row (Right/Left) of the field and can pick the outermost crop on that line.
    /// </summary>
    public struct Station
    {
        public Side Side;
        public int Line;

        public Station(Side side, int line)
        {
            Side = side;
            Line = line;
        }
    }

    /// <summary>One crop picked during a lap: where the harvester was and which cell it emptied.</summary>
    public struct Pick
    {
        public int Station;
        public int X;
        public int Y;

        public Pick(int station, int x, int y)
        {
            Station = station;
            X = x;
            Y = y;
        }
    }

    public enum GameStatus { Playing, Won, Lost }

    /// <summary>What happened when a harvester drove a lap. Used for animation and previews.</summary>
    public class LapResult
    {
        public Harvester Start;
        public bool FromBay;
        /// <summary>Lane index, or bay index when FromBay.</summary>
        public int Source;
        public readonly List<Pick> Picks = new List<Pick>();
        /// <summary>Station index where the harvester stopped: where it filled up, or Stations.Count after a full lap.</summary>
        public int EndStation;
        /// <summary>Capacity left after the lap. Zero means the harvester filled up and drove home.</summary>
        public int Remaining;
        /// <summary>Bay the harvester parked in, or -1.</summary>
        public int ParkedBay = -1;
        /// <summary>True when there was no bay to park in; the level is lost unless revived.</summary>
        public bool Overflow;

        public bool Filled { get { return Remaining == 0; } }
        public bool Combo { get { return Picks.Count >= HarvestState.ComboPicks; } }
    }

    /// <summary>
    /// Rules of the farm-harvest puzzle.
    /// The field is a grid of crops with a road loop around it. The garage has lanes of harvesters;
    /// tapping a lane sends its front harvester once around the loop. At every station it picks the
    /// outermost crop on that line if the crop matches its color. When it is full it drives home.
    /// If it still has room after the lap it parks in a bay; parked harvesters can be sent again, and
    /// the merge booster joins parked harvesters of the same color. Clear the field to win; a harvester
    /// with nowhere to park loses the level.
    /// </summary>
    public class HarvestState
    {
        public const int ComboPicks = 5;
        public const int FreeUndos = 3;
        public const int FreeMerges = 1;

        public readonly FieldLevel Level;
        public readonly int Width;
        public readonly int Height;
        public readonly List<Station> Stations = new List<Station>();

        readonly Harvester[][] laneQueues;
        int[] crops;
        int[] laneHeads;
        Harvester[] bays;

        public GameStatus Status { get; private set; }
        public int CropsLeft { get; private set; }
        public int Moves { get; private set; }
        public int UndosLeft { get; private set; }
        public int MergesLeft { get; private set; }
        public bool UndoUsed { get; private set; }
        public bool Revived { get; private set; }
        public int BestCombo { get; private set; }
        /// <summary>The harvester that had nowhere to park, while Status is Lost.</summary>
        public Harvester Overflowed { get; private set; }

        struct Snapshot
        {
            public int[] Crops;
            public int[] LaneHeads;
            public Harvester[] Bays;
            public int CropsLeft;
            public int Moves;
            public GameStatus Status;
            public Harvester Overflowed;
        }

        readonly List<Snapshot> history = new List<Snapshot>();

        public HarvestState(FieldLevel level)
        {
            Level = level;
            Width = level.Width;
            Height = level.Height;

            crops = new int[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int crop = level.CropAt(x, y);
                    crops[y * Width + x] = crop;
                    if (crop != Crops.None) CropsLeft++;
                }
            }

            laneQueues = new Harvester[level.lanes.Length][];
            for (int i = 0; i < laneQueues.Length; i++) laneQueues[i] = FieldLevel.ParseLane(level.lanes[i]).ToArray();
            laneHeads = new int[laneQueues.Length];
            bays = new Harvester[level.bays];

            for (int x = 0; x < Width; x++) Stations.Add(new Station(Side.Bottom, x));
            for (int y = 0; y < Height; y++) Stations.Add(new Station(Side.Right, y));
            for (int x = Width - 1; x >= 0; x--) Stations.Add(new Station(Side.Top, x));
            for (int y = Height - 1; y >= 0; y--) Stations.Add(new Station(Side.Left, y));

            UndosLeft = FreeUndos;
            MergesLeft = FreeMerges;
            Status = CropsLeft == 0 ? GameStatus.Won : GameStatus.Playing;
        }

        HarvestState(HarvestState other)
        {
            Level = other.Level;
            Width = other.Width;
            Height = other.Height;
            Stations = other.Stations;
            laneQueues = other.laneQueues;
            crops = (int[])other.crops.Clone();
            laneHeads = (int[])other.laneHeads.Clone();
            bays = (Harvester[])other.bays.Clone();
            Status = other.Status;
            CropsLeft = other.CropsLeft;
            Moves = other.Moves;
            UndosLeft = other.UndosLeft;
            MergesLeft = other.MergesLeft;
            UndoUsed = other.UndoUsed;
            Revived = other.Revived;
            BestCombo = other.BestCombo;
            Overflowed = other.Overflowed;
        }

        /// <summary>A copy without undo history, for solvers and previews.</summary>
        public HarvestState Clone()
        {
            return new HarvestState(this);
        }

        // ---- Queries -------------------------------------------------------------------------

        public int LaneCount { get { return laneQueues.Length; } }
        public int BayCount { get { return bays.Length; } }

        public int CropAt(int x, int y)
        {
            return crops[y * Width + x];
        }

        public bool LaneEmpty(int lane)
        {
            return laneHeads[lane] >= laneQueues[lane].Length;
        }

        /// <summary>Harvesters still waiting in a lane, front first.</summary>
        public List<Harvester> LaneQueue(int lane)
        {
            var result = new List<Harvester>();
            for (int i = laneHeads[lane]; i < laneQueues[lane].Length; i++) result.Add(laneQueues[lane][i]);
            return result;
        }

        public Harvester Bay(int bay)
        {
            return bays[bay];
        }

        public int FreeBays
        {
            get
            {
                int free = 0;
                foreach (var b in bays) if (b.IsEmpty) free++;
                return free;
            }
        }

        public bool CanUndo { get { return history.Count > 0 && UndosLeft > 0; } }

        /// <summary>3 stars for a clean run, 2 if undo was used, 1 if the level needed a revive.</summary>
        public int Stars
        {
            get
            {
                if (Status != GameStatus.Won) return 0;
                if (Revived) return 1;
                return UndoUsed ? 2 : 3;
            }
        }

        /// <summary>The outermost crop seen from a station, or (-1, -1) if the line is bare.</summary>
        public void Outermost(Station station, out int x, out int y)
        {
            Outermost(crops, station, out x, out y);
        }

        void Outermost(int[] grid, Station station, out int x, out int y)
        {
            switch (station.Side)
            {
                case Side.Bottom:
                    x = station.Line;
                    for (y = 0; y < Height; y++) if (grid[y * Width + x] != Crops.None) return;
                    break;
                case Side.Top:
                    x = station.Line;
                    for (y = Height - 1; y >= 0; y--) if (grid[y * Width + x] != Crops.None) return;
                    break;
                case Side.Right:
                    y = station.Line;
                    for (x = Width - 1; x >= 0; x--) if (grid[y * Width + x] != Crops.None) return;
                    break;
                default:
                    y = station.Line;
                    for (x = 0; x < Width; x++) if (grid[y * Width + x] != Crops.None) return;
                    break;
            }
            x = -1;
            y = -1;
        }

        // ---- Moves ---------------------------------------------------------------------------

        public bool CanDispatchLane(int lane)
        {
            return Status == GameStatus.Playing && lane >= 0 && lane < LaneCount && !LaneEmpty(lane);
        }

        public bool CanDispatchBay(int bay)
        {
            return Status == GameStatus.Playing && bay >= 0 && bay < BayCount && !bays[bay].IsEmpty;
        }

        /// <summary>What a dispatch would do, without changing anything. Null if the move is not allowed.</summary>
        public LapResult Preview(bool fromBay, int index)
        {
            if (fromBay ? !CanDispatchBay(index) : !CanDispatchLane(index)) return null;
            return Clone().Dispatch(fromBay, index, false);
        }

        public LapResult DispatchLane(int lane)
        {
            return CanDispatchLane(lane) ? Dispatch(false, lane, true) : null;
        }

        public LapResult DispatchBay(int bay)
        {
            return CanDispatchBay(bay) ? Dispatch(true, bay, true) : null;
        }

        LapResult Dispatch(bool fromBay, int index, bool record)
        {
            if (record) Save();

            var result = new LapResult { FromBay = fromBay, Source = index };
            Harvester h;
            if (fromBay)
            {
                h = bays[index];
                bays[index] = default(Harvester);
            }
            else
            {
                h = laneQueues[index][laneHeads[index]];
                laneHeads[index]++;
            }
            result.Start = h;

            int s = 0;
            for (; s < Stations.Count && h.Capacity > 0; s++)
            {
                int x, y;
                Outermost(crops, Stations[s], out x, out y);
                if (x < 0 || crops[y * Width + x] != h.Color) continue;
                crops[y * Width + x] = Crops.None;
                CropsLeft--;
                h.Capacity--;
                result.Picks.Add(new Pick(s, x, y));
            }
            result.EndStation = h.Capacity == 0 ? s - 1 : Stations.Count;
            result.Remaining = h.Capacity;
            Moves++;
            if (result.Picks.Count > BestCombo) BestCombo = result.Picks.Count;

            if (h.Capacity > 0)
            {
                int bay = FindBay();
                if (bay < 0)
                {
                    result.Overflow = true;
                    Overflowed = h;
                    Status = GameStatus.Lost;
                    return result;
                }
                result.ParkedBay = bay;
                bays[bay] = h;
            }

            if (CropsLeft == 0) Status = GameStatus.Won;
            return result;
        }

        int FindBay()
        {
            for (int i = 0; i < bays.Length; i++) if (bays[i].IsEmpty) return i;
            return -1;
        }

        /// <summary>
        /// Booster: parked harvesters of the same color join up, freeing bays. Limited per level.
        /// Returns the bays that were emptied.
        /// </summary>
        public List<int> MergeBays()
        {
            var freed = new List<int>();
            if (!CanMerge) return freed;
            Save();
            for (int i = 0; i < bays.Length; i++)
            {
                if (bays[i].IsEmpty) continue;
                for (int j = i + 1; j < bays.Length; j++)
                {
                    if (bays[j].IsEmpty || bays[j].Color != bays[i].Color) continue;
                    bays[i].Capacity += bays[j].Capacity;
                    bays[j] = default(Harvester);
                    freed.Add(j);
                }
            }
            MergesLeft--;
            return freed;
        }

        public bool CanMerge
        {
            get
            {
                if (Status != GameStatus.Playing || MergesLeft <= 0) return false;
                for (int i = 0; i < bays.Length; i++)
                    for (int j = i + 1; j < bays.Length; j++)
                        if (!bays[i].IsEmpty && !bays[j].IsEmpty && bays[i].Color == bays[j].Color) return true;
                return false;
            }
        }

        /// <summary>After an overflow: build one more bay and park the stranded harvester in it.</summary>
        public bool Revive()
        {
            if (Status != GameStatus.Lost || Revived) return false;
            var grown = new Harvester[bays.Length + 1];
            Array.Copy(bays, grown, bays.Length);
            grown[bays.Length] = Overflowed;
            bays = grown;
            Overflowed = default(Harvester);
            Revived = true;
            Status = GameStatus.Playing;
            return true;
        }

        void Save()
        {
            history.Add(new Snapshot
            {
                Crops = (int[])crops.Clone(),
                LaneHeads = (int[])laneHeads.Clone(),
                Bays = (Harvester[])bays.Clone(),
                CropsLeft = CropsLeft,
                Moves = Moves,
                Status = Status,
                Overflowed = Overflowed,
            });
        }

        /// <summary>Takes back the last dispatch, also after losing. Limited to FreeUndos per level.</summary>
        public bool Undo()
        {
            if (!CanUndo || Status == GameStatus.Won) return false;
            var s = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            crops = s.Crops;
            laneHeads = s.LaneHeads;
            // Bays built by a revive stay built.
            var restored = new Harvester[Math.Max(bays.Length, s.Bays.Length)];
            Array.Copy(s.Bays, restored, s.Bays.Length);
            bays = restored;
            CropsLeft = s.CropsLeft;
            Moves = s.Moves;
            Status = s.Status;
            Overflowed = s.Overflowed;
            UndosLeft--;
            UndoUsed = true;
            return true;
        }

        /// <summary>Identifies a position for solvers: field, lane progress and the bay contents (order ignored).</summary>
        public string Key()
        {
            var sb = new StringBuilder(crops.Length + 32);
            foreach (int c in crops) sb.Append(Crops.ToLetter(c));
            sb.Append('|');
            foreach (int head in laneHeads) sb.Append(head).Append(',');
            sb.Append('|');
            var parked = new List<string>();
            foreach (var b in bays) if (!b.IsEmpty) parked.Add(b.ToString());
            parked.Sort(StringComparer.Ordinal);
            sb.Append(string.Join(",", parked.ToArray()));
            return sb.ToString();
        }
    }
}
