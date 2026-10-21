using System.Collections.Generic;
using System.Text;

namespace ParcelSort.Core
{
    public enum GameStatus { Playing, Won, Lost }

    public struct Parcel
    {
        public int Color;
        public bool Hidden;

        public Parcel(int color, bool hidden)
        {
            Color = color;
            Hidden = hidden;
        }
    }

    public struct BufferedParcel
    {
        public int Color;
        public int OriginStack;
    }

    public class Dock
    {
        /// <summary>Color of the truck parked here, or ParcelColors.None when the dock is empty.</summary>
        public int Color = ParcelColors.None;
        public int Load;
        public bool IsEmpty { get { return Color == ParcelColors.None; } }
    }

    /// <summary>What a single tap did, so the view can animate it.</summary>
    public class MoveResult
    {
        public int Stack;
        public int Color;
        /// <summary>Dock the parcel went to, or -1 if it went to the buffer.</summary>
        public int ToDock = -1;
        /// <summary>Docks whose truck filled up and left, in order.</summary>
        public List<int> Departures = new List<int>();
        /// <summary>Number of buffered parcels that were loaded onto newly arrived trucks.</summary>
        public int BufferLoads;
    }

    /// <summary>
    /// Rules of the parcel-sorting puzzle, with no Unity dependency.
    /// Tap the top parcel of a stack: it goes onto a parked truck of the same color if one has room,
    /// otherwise onto the buffer shelf. A full truck leaves and the next one in the queue parks.
    /// Newly parked trucks take matching parcels from the buffer. Clear every parcel to win;
    /// the level is lost when the buffer is full.
    /// </summary>
    public class GameState
    {
        public readonly LevelData Level;
        public readonly List<List<Parcel>> Stacks = new List<List<Parcel>>();
        public readonly List<BufferedParcel> Buffer = new List<BufferedParcel>();
        public readonly Dock[] Docks;
        public int BufferCapacity;
        public int NextTruck;
        public GameStatus Status = GameStatus.Playing;
        public int MaxBufferUsed;
        public int Moves;
        public bool ExtraSlotUsed;

        public bool RecordHistory = true;
        readonly Stack<GameState> history = new Stack<GameState>();

        public GameState(LevelData level)
        {
            Level = level;
            BufferCapacity = level.bufferSize;
            Docks = new Dock[level.dockCount];
            for (int i = 0; i < Docks.Length; i++) Docks[i] = new Dock();

            foreach (string column in level.stacks)
            {
                var stack = new List<Parcel>();
                foreach (char c in column) stack.Add(new Parcel(ParcelColors.FromLetter(c), char.IsUpper(c)));
                Stacks.Add(stack);
                RevealTop(stack);
            }

            ResolveDocks(new MoveResult());
            UpdateStatus();
        }

        GameState(GameState other)
        {
            Level = other.Level;
            foreach (var stack in other.Stacks) Stacks.Add(new List<Parcel>(stack));
            Buffer.AddRange(other.Buffer);
            Docks = new Dock[other.Docks.Length];
            for (int i = 0; i < Docks.Length; i++) Docks[i] = new Dock { Color = other.Docks[i].Color, Load = other.Docks[i].Load };
            BufferCapacity = other.BufferCapacity;
            NextTruck = other.NextTruck;
            Status = other.Status;
            MaxBufferUsed = other.MaxBufferUsed;
            Moves = other.Moves;
            ExtraSlotUsed = other.ExtraSlotUsed;
            RecordHistory = false;
        }

        /// <summary>Copy of the current position without undo history.</summary>
        public GameState Clone()
        {
            return new GameState(this);
        }

        public int TruckCapacity { get { return Level.truckCapacity; } }
        public int TrucksRemaining { get { return Level.trucks.Length - NextTruck; } }
        public bool CanUndo { get { return history.Count > 0; } }

        public bool CanTap(int stack)
        {
            return Status == GameStatus.Playing && stack >= 0 && stack < Stacks.Count && Stacks[stack].Count > 0;
        }

        /// <summary>Taps the top parcel of a stack. Returns null if the tap is not allowed.</summary>
        public MoveResult Tap(int stack)
        {
            if (!CanTap(stack)) return null;
            if (RecordHistory) history.Push(Clone());

            var column = Stacks[stack];
            var parcel = column[column.Count - 1];
            column.RemoveAt(column.Count - 1);
            RevealTop(column);
            Moves++;

            var result = new MoveResult { Stack = stack, Color = parcel.Color };
            int dock = FindDockWithRoom(parcel.Color);
            if (dock >= 0)
            {
                Docks[dock].Load++;
                result.ToDock = dock;
            }
            else
            {
                Buffer.Add(new BufferedParcel { Color = parcel.Color, OriginStack = stack });
                if (Buffer.Count > MaxBufferUsed) MaxBufferUsed = Buffer.Count;
            }

            ResolveDocks(result);
            UpdateStatus();
            return result;
        }

        public bool Undo()
        {
            if (history.Count == 0) return false;
            CopyFrom(history.Pop());
            return true;
        }

        /// <summary>Booster: one more buffer slot for the rest of the level.</summary>
        public bool AddBufferSlot()
        {
            if (ExtraSlotUsed || Status == GameStatus.Won) return false;
            if (RecordHistory) history.Push(Clone());
            ExtraSlotUsed = true;
            BufferCapacity++;
            UpdateStatus();
            return true;
        }

        /// <summary>
        /// Second chance after losing: puts the most recently buffered parcels back on top of
        /// the stacks they came from.
        /// </summary>
        public bool Revive(int count = 3)
        {
            if (Status != GameStatus.Lost) return false;
            for (int i = 0; i < count && Buffer.Count > 0; i++)
            {
                var item = Buffer[Buffer.Count - 1];
                Buffer.RemoveAt(Buffer.Count - 1);
                Stacks[item.OriginStack].Add(new Parcel(item.Color, false));
            }
            UpdateStatus();
            return true;
        }

        /// <summary>1 to 3 stars; fewer buffer slots used is better.</summary>
        public int Stars
        {
            get
            {
                if (Status != GameStatus.Won) return 0;
                if (MaxBufferUsed <= Level.bufferSize / 2) return 3;
                if (MaxBufferUsed < Level.bufferSize) return 2;
                return 1;
            }
        }

        /// <summary>A key that identifies the position, for the solver's memo table.</summary>
        public string PositionKey()
        {
            var sb = new StringBuilder();
            foreach (var stack in Stacks) sb.Append((char)('0' + stack.Count));
            sb.Append('|').Append(NextTruck).Append('|');
            foreach (var dock in Docks) sb.Append(dock.Color + 1).Append(dock.Load);
            sb.Append('|');
            var colors = new int[ParcelColors.Count];
            foreach (var item in Buffer) colors[item.Color]++;
            foreach (int n in colors) sb.Append(n);
            sb.Append('|').Append(BufferCapacity);
            return sb.ToString();
        }

        int FindDockWithRoom(int color)
        {
            for (int i = 0; i < Docks.Length; i++)
            {
                if (Docks[i].Color == color && Docks[i].Load < TruckCapacity) return i;
            }
            return -1;
        }

        void ResolveDocks(MoveResult result)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < Docks.Length; i++)
                {
                    var dock = Docks[i];
                    if (!dock.IsEmpty && dock.Load >= TruckCapacity)
                    {
                        result.Departures.Add(i);
                        dock.Color = ParcelColors.None;
                        dock.Load = 0;
                        changed = true;
                    }
                    if (dock.IsEmpty && NextTruck < Level.trucks.Length)
                    {
                        dock.Color = ParcelColors.FromLetter(Level.trucks[NextTruck++]);
                        dock.Load = 0;
                        changed = true;
                    }
                    if (!dock.IsEmpty)
                    {
                        for (int b = 0; b < Buffer.Count && dock.Load < TruckCapacity;)
                        {
                            if (Buffer[b].Color == dock.Color)
                            {
                                Buffer.RemoveAt(b);
                                dock.Load++;
                                result.BufferLoads++;
                                changed = true;
                            }
                            else b++;
                        }
                    }
                }
            }
        }

        void UpdateStatus()
        {
            bool stacksEmpty = true;
            foreach (var stack in Stacks) if (stack.Count > 0) stacksEmpty = false;

            if (stacksEmpty && Buffer.Count == 0) Status = GameStatus.Won;
            else if (Buffer.Count >= BufferCapacity || stacksEmpty) Status = GameStatus.Lost;
            else Status = GameStatus.Playing;
        }

        static void RevealTop(List<Parcel> stack)
        {
            if (stack.Count == 0) return;
            var top = stack[stack.Count - 1];
            if (!top.Hidden) return;
            top.Hidden = false;
            stack[stack.Count - 1] = top;
        }

        void CopyFrom(GameState other)
        {
            Stacks.Clear();
            foreach (var stack in other.Stacks) Stacks.Add(new List<Parcel>(stack));
            Buffer.Clear();
            Buffer.AddRange(other.Buffer);
            for (int i = 0; i < Docks.Length; i++)
            {
                Docks[i].Color = other.Docks[i].Color;
                Docks[i].Load = other.Docks[i].Load;
            }
            BufferCapacity = other.BufferCapacity;
            NextTruck = other.NextTruck;
            Status = other.Status;
            MaxBufferUsed = other.MaxBufferUsed;
            Moves = other.Moves;
            ExtraSlotUsed = other.ExtraSlotUsed;
        }
    }
}
