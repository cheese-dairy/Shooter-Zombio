using System;
using System.Collections.Generic;

namespace Zombio
{
    /// <summary>Integer X/Z coordinates shared by the generator and dependency-free pathfinder.</summary>
    public struct GridCell : IEquatable<GridCell>
    {
        public int X;
        public int Z;
        public GridCell(int x, int z) { X = x; Z = z; }
        public bool Equals(GridCell other) { return X == other.X && Z == other.Z; }
        public override bool Equals(object obj) { return obj is GridCell && Equals((GridCell)obj); }
        public override int GetHashCode() { unchecked { return (X * 397) ^ Z; } }
        public static bool operator ==(GridCell a, GridCell b) { return a.Equals(b); }
        public static bool operator !=(GridCell a, GridCell b) { return !a.Equals(b); }
        public override string ToString() { return "(" + X + ", " + Z + ")"; }
    }

    /// <summary>
    /// A* over an immutable copy of a walkability grid. Eight-way paths never cut
    /// blocked corners. Returned paths include both endpoints; no route returns an empty list.
    /// All scratch storage belongs to the search, so concurrent callers are safe.
    /// </summary>
    public sealed class GridNavigation
    {
        private readonly bool[,] walkable;
        public int Width { get { return walkable.GetLength(0); } }
        public int Height { get { return walkable.GetLength(1); } }

        public GridNavigation(bool[,] cells)
        {
            if (cells == null) throw new ArgumentNullException("cells");
            if (cells.GetLength(0) == 0 || cells.GetLength(1) == 0)
                throw new ArgumentException("A navigation grid must have positive dimensions.", "cells");
            walkable = (bool[,])cells.Clone();
        }

        public bool IsWalkable(GridCell cell)
        {
            return cell.X >= 0 && cell.Z >= 0 && cell.X < Width && cell.Z < Height && walkable[cell.X, cell.Z];
        }

        public List<GridCell> FindPath(GridCell start, GridCell goal)
        {
            var result = new List<GridCell>();
            if (!IsWalkable(start) || !IsWalkable(goal)) return result;
            if (start == goal) { result.Add(start); return result; }

            int count = Width * Height;
            var cost = new int[count];
            var parent = new int[count];
            var closed = new bool[count];
            for (int i = 0; i < count; i++) { cost[i] = int.MaxValue; parent[i] = -1; }
            var open = new MinHeap();
            int startId = Index(start.X, start.Z);
            int goalId = Index(goal.X, goal.Z);
            cost[startId] = 0;
            open.Push(startId, Heuristic(start.X, start.Z, goal), 0);

            while (open.Count > 0)
            {
                Entry next = open.Pop();
                int id = next.Id;
                if (closed[id] || next.Cost != cost[id]) continue;
                if (id == goalId)
                {
                    for (int cursor = goalId; cursor != -1; cursor = parent[cursor])
                        result.Add(new GridCell(cursor % Width, cursor / Width));
                    result.Reverse();
                    return result;
                }
                closed[id] = true;
                int x = id % Width;
                int z = id / Width;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var neighbor = new GridCell(x + dx, z + dz);
                    if (!IsWalkable(neighbor)) continue;
                    bool diagonal = dx != 0 && dz != 0;
                    if (diagonal && (!walkable[x + dx, z] || !walkable[x, z + dz])) continue;
                    int neighborId = Index(neighbor.X, neighbor.Z);
                    if (closed[neighborId]) continue;
                    int candidate = cost[id] + (diagonal ? 14 : 10);
                    if (candidate >= cost[neighborId]) continue;
                    cost[neighborId] = candidate;
                    parent[neighborId] = id;
                    open.Push(neighborId, candidate + Heuristic(neighbor.X, neighbor.Z, goal), candidate);
                }
            }
            return result;
        }

        private int Index(int x, int z) { return z * Width + x; }
        private static int Heuristic(int x, int z, GridCell goal)
        {
            int dx = Math.Abs(x - goal.X);
            int dz = Math.Abs(z - goal.Z);
            return 14 * Math.Min(dx, dz) + 10 * Math.Abs(dx - dz);
        }

        private struct Entry
        {
            public int Id, Priority, Cost;
            public Entry(int id, int priority, int cost) { Id = id; Priority = priority; Cost = cost; }
        }

        private sealed class MinHeap
        {
            private readonly List<Entry> items = new List<Entry>();
            public int Count { get { return items.Count; } }
            public void Push(int id, int priority, int cost)
            {
                var entry = new Entry(id, priority, cost);
                items.Add(entry);
                int at = items.Count - 1;
                while (at > 0)
                {
                    int above = (at - 1) / 2;
                    if (items[above].Priority <= priority) break;
                    items[at] = items[above];
                    at = above;
                }
                items[at] = entry;
            }
            public Entry Pop()
            {
                Entry first = items[0];
                Entry tail = items[items.Count - 1];
                items.RemoveAt(items.Count - 1);
                if (items.Count == 0) return first;
                int at = 0;
                while (true)
                {
                    int child = at * 2 + 1;
                    if (child >= items.Count) break;
                    if (child + 1 < items.Count && items[child + 1].Priority < items[child].Priority) child++;
                    if (tail.Priority <= items[child].Priority) break;
                    items[at] = items[child];
                    at = child;
                }
                items[at] = tail;
                return first;
            }
        }
    }
}
