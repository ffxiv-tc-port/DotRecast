using System;
using System.Collections.Generic;

namespace DotRecast.Recast
{
    // Working arrays reused from one tile to the next by a single build thread (not thread-safe).
    // A compact heightfield built with a scratch borrows its arrays from it, and so does every later step run on it,
    // so it is only valid until the scratch is used for the next tile. Arrays handed out may be longer than asked for;
    // their first `length` elements are zero like a fresh `new T[length]`, and callers never read past `length`.
    public sealed class RcBuildScratch
    {
        // AREAS and DIST belong to the compact heightfield; the rest are temporaries that only live inside one step,
        // so steps share them (a step that needs two at once gets two different ones).
        internal const int AREAS = 0;
        internal const int DIST = 1;
        internal const int ERODE_DIST = 2;
        internal const int DIST_TEMP = 2;
        internal const int REGION_REG = 2;
        internal const int REGION_DIST = 3;
        internal const int CONTOUR_FLAGS = 2;

        private readonly int[][] _ints = new int[4][];
        private RcCompactCell[] _cells;
        private RcCompactSpan[] _spans;
        private readonly List<List<RcLevelStackEntry>> _levelStacks = new List<List<RcLevelStackEntry>>();
        private readonly List<RcLevelStackEntry> _stack = new List<RcLevelStackEntry>(256);
        private readonly List<RcDirtyEntry> _dirtyEntries = new List<RcDirtyEntry>();

        internal int[] Ints(int slot, int length) => Zeroed(ref _ints[slot], length);
        internal RcCompactCell[] Cells(int length) => Zeroed(ref _cells, length);
        internal RcCompactSpan[] Spans(int length) => Zeroed(ref _spans, length);

        // The lists below come back empty.
        internal List<List<RcLevelStackEntry>> LevelStacks(int count)
        {
            if (_levelStacks.Count != count)
            {
                _levelStacks.Clear();
                for (int i = 0; i < count; ++i)
                    _levelStacks.Add(new List<RcLevelStackEntry>(256));
            }

            foreach (var s in _levelStacks)
                s.Clear();
            return _levelStacks;
        }

        internal List<RcLevelStackEntry> Stack()
        {
            _stack.Clear();
            return _stack;
        }

        internal List<RcDirtyEntry> DirtyEntries()
        {
            _dirtyEntries.Clear();
            return _dirtyEntries;
        }

        private static T[] Zeroed<T>(ref T[] array, int length)
        {
            if (array == null || array.Length < length)
                array = new T[length];
            else
                Array.Clear(array, 0, length);
            return array;
        }
    }
}
