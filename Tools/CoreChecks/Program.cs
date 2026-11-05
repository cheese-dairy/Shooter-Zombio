using System;
using System.Collections.Generic;
using Zombio;

internal static class Program
{
    private static int passed;
    private static int failed;

    private static int Main()
    {
        Run("path includes both endpoints and uses optimal diagonals", OpenGrid);
        Run("blocked corners cannot be crossed diagonally", BlockedCorner);
        Run("navigation routes through a wall opening", WallDetour);
        Run("disconnected cells return no route", Unreachable);
        Run("out-of-bounds and blocked endpoints return no route", InvalidEndpoints);
        Run("same walkable start and end returns one cell", SameEndpoint);
        Run("navigation owns an immutable grid snapshot", ImmutableGrid);
        Run("invalid constructor grids throw", InvalidGrid);
        Run("repeated queries produce deterministic paths", DeterministicPaths);
        Run("180 random routes match independent shortest-path solver", RandomizedPaths);
        WaveChecks();
        Console.WriteLine("\nCore checks: " + passed + " passed, " + failed + " failed.");
        Console.WriteLine("These checks execute shared production C# logic; they do not run Unity physics, rendering, input, audio, or play mode.");
        return failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static bool[,] Open(int width, int height)
    {
        var grid = new bool[width, height];
        for (int z = 0; z < height; z++)
        for (int x = 0; x < width; x++) grid[x, z] = true;
        return grid;
    }

    private static void OpenGrid()
    {
        var grid = Open(8, 6);
        var path = new GridNavigation(grid).FindPath(new GridCell(0, 0), new GridCell(7, 5));
        ValidatePath(grid, path, new GridCell(0, 0), new GridCell(7, 5));
        Assert(PathCost(path) == 90, "Expected five diagonal and two cardinal steps.");
    }

    private static void BlockedCorner()
    {
        var grid = Open(2, 2);
        grid[1, 0] = grid[0, 1] = false;
        Assert(new GridNavigation(grid).FindPath(new GridCell(0, 0), new GridCell(1, 1)).Count == 0,
            "Diagonal movement crossed a blocked corner.");
        grid[0, 1] = true;
        var path = new GridNavigation(grid).FindPath(new GridCell(0, 0), new GridCell(1, 1));
        ValidatePath(grid, path, new GridCell(0, 0), new GridCell(1, 1));
        Assert(path.Count == 3, "One blocked side must force two cardinal steps.");
    }

    private static void WallDetour()
    {
        var grid = Open(9, 9);
        for (int z = 0; z < 8; z++) grid[4, z] = false;
        var path = new GridNavigation(grid).FindPath(new GridCell(1, 1), new GridCell(7, 1));
        ValidatePath(grid, path, new GridCell(1, 1), new GridCell(7, 1));
        Assert(path.Contains(new GridCell(4, 8)), "Route missed the only opening.");
    }

    private static void Unreachable()
    {
        var grid = Open(7, 7);
        for (int z = 0; z < 7; z++) grid[3, z] = false;
        Assert(new GridNavigation(grid).FindPath(new GridCell(1, 1), new GridCell(5, 1)).Count == 0,
            "A full blocking wall must disconnect the grid.");
    }

    private static void InvalidEndpoints()
    {
        var grid = Open(4, 4);
        grid[2, 2] = false;
        var navigation = new GridNavigation(grid);
        var invalid = new[] { new GridCell(-1, 0), new GridCell(4, 0), new GridCell(0, -1), new GridCell(0, 4), new GridCell(2, 2) };
        foreach (var cell in invalid)
        {
            Assert(navigation.FindPath(cell, new GridCell(1, 1)).Count == 0, "Invalid start accepted: " + cell);
            Assert(navigation.FindPath(new GridCell(1, 1), cell).Count == 0, "Invalid goal accepted: " + cell);
        }
    }

    private static void SameEndpoint()
    {
        var path = new GridNavigation(Open(2, 2)).FindPath(new GridCell(1, 1), new GridCell(1, 1));
        Assert(path.Count == 1 && path[0] == new GridCell(1, 1), "Incorrect stationary path.");
    }

    private static void ImmutableGrid()
    {
        var grid = Open(3, 1);
        var navigation = new GridNavigation(grid);
        grid[1, 0] = false;
        Assert(navigation.FindPath(new GridCell(0, 0), new GridCell(2, 0)).Count == 3,
            "External mutation unexpectedly changed navigation.");
    }

    private static void InvalidGrid()
    {
        bool nullThrows = false, emptyThrows = false;
        try { new GridNavigation(null); } catch (ArgumentNullException) { nullThrows = true; }
        try { new GridNavigation(new bool[0, 3]); } catch (ArgumentException) { emptyThrows = true; }
        Assert(nullThrows && emptyThrows, "Expected explicit rejection of missing/empty grids.");
    }

    private static void DeterministicPaths()
    {
        var grid = Open(12, 12);
        for (int i = 2; i < 9; i++) grid[5, i] = false;
        var navigation = new GridNavigation(grid);
        var baseline = navigation.FindPath(new GridCell(1, 5), new GridCell(10, 5));
        for (int repetition = 0; repetition < 30; repetition++)
        {
            var actual = navigation.FindPath(new GridCell(1, 5), new GridCell(10, 5));
            Assert(actual.Count == baseline.Count, "Path length changed between queries.");
            for (int i = 0; i < actual.Count; i++) Assert(actual[i] == baseline[i], "Path changed between queries.");
        }
    }

    private static void RandomizedPaths()
    {
        var random = new Random(704123);
        for (int trial = 0; trial < 180; trial++)
        {
            var grid = new bool[13, 11];
            for (int z = 0; z < 11; z++)
            for (int x = 0; x < 13; x++) grid[x, z] = random.NextDouble() > .28;
            var start = new GridCell(random.Next(13), random.Next(11));
            var end = new GridCell(random.Next(13), random.Next(11));
            grid[start.X, start.Z] = grid[end.X, end.Z] = true;
            var actual = new GridNavigation(grid).FindPath(start, end);
            int expected = ReferenceCost(grid, start, end);
            if (expected == int.MaxValue) Assert(actual.Count == 0, "Trial " + trial + " found impossible route.");
            else
            {
                ValidatePath(grid, actual, start, end);
                Assert(PathCost(actual) == expected, "Trial " + trial + " returned non-optimal route.");
            }
        }
    }

    private static void ValidatePath(bool[,] grid, List<GridCell> path, GridCell start, GridCell goal)
    {
        Assert(path.Count > 0, "Expected a route.");
        Assert(path[0] == start && path[path.Count - 1] == goal, "Path endpoints incorrect.");
        for (int i = 0; i < path.Count; i++)
        {
            GridCell cell = path[i];
            Assert(grid[cell.X, cell.Z], "Path entered blocked cell.");
            if (i == 0) continue;
            GridCell previous = path[i - 1];
            int dx = Math.Abs(previous.X - cell.X), dz = Math.Abs(previous.Z - cell.Z);
            Assert(dx <= 1 && dz <= 1 && dx + dz > 0, "Path jumped or repeated a cell.");
            if (dx == 1 && dz == 1)
                Assert(grid[previous.X, cell.Z] && grid[cell.X, previous.Z], "Path cut a blocked corner.");
        }
    }

    private static int PathCost(List<GridCell> path)
    {
        int cost = 0;
        for (int i = 1; i < path.Count; i++)
            cost += path[i].X != path[i - 1].X && path[i].Z != path[i - 1].Z ? 14 : 10;
        return cost;
    }

    // Deliberately simple O(V²) Dijkstra oracle, independent of the production A* heap/heuristic.
    private static int ReferenceCost(bool[,] grid, GridCell start, GridCell end)
    {
        int width = grid.GetLength(0), height = grid.GetLength(1);
        var distances = new int[width, height];
        var visited = new bool[width, height];
        for (int z = 0; z < height; z++)
        for (int x = 0; x < width; x++) distances[x, z] = int.MaxValue;
        distances[start.X, start.Z] = 0;
        for (int iteration = 0; iteration < width * height; iteration++)
        {
            int lowest = int.MaxValue, bx = -1, bz = -1;
            for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
                if (!visited[x, z] && distances[x, z] < lowest) { lowest = distances[x, z]; bx = x; bz = z; }
            if (bx == -1) return int.MaxValue;
            if (bx == end.X && bz == end.Z) return lowest;
            visited[bx, bz] = true;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = bx + dx, z = bz + dz;
                if (dx == 0 && dz == 0 || x < 0 || z < 0 || x >= width || z >= height || !grid[x, z]) continue;
                if (dx != 0 && dz != 0 && (!grid[x, bz] || !grid[bx, z])) continue;
                int candidate = lowest + (dx != 0 && dz != 0 ? 14 : 10);
                if (candidate < distances[x, z]) distances[x, z] = candidate;
            }
        }
        return int.MaxValue;
    }

    private static void WaveChecks()
    {
        Run("wave counts increase gradually and stop at configured cap", () =>
        {
            var rules = new WaveRules();
            Assert(rules.GetEnemyCount(1) == 6 && rules.GetEnemyCount(2) == 9, "Opening wave counts changed unexpectedly.");
            int previous = 0;
            for (int wave = 1; wave <= 1000; wave++)
            {
                int count = rules.GetEnemyCount(wave);
                Assert(count >= previous && count <= rules.MaximumCount, "Non-monotonic or unbounded wave count at " + wave);
                previous = count;
            }
            Assert(rules.GetEnemyCount(int.MaxValue) == rules.MaximumCount, "Late count overflowed.");
        });
        Run("enemy health and damage scale within fair configured caps", () =>
        {
            var rules = new WaveRules();
            Assert(rules.GetHealthMultiplier(1) == 1f && rules.GetDamageMultiplier(1) == 1f, "First wave must use base stats.");
            float previousHealth = 1, previousDamage = 1;
            for (int wave = 1; wave <= 1000; wave++)
            {
                float health = rules.GetHealthMultiplier(wave), damage = rules.GetDamageMultiplier(wave);
                Assert(health >= previousHealth && health <= rules.MaximumHealthMultiplier, "Health scaling exceeded bounds.");
                Assert(damage >= previousDamage && damage <= rules.MaximumDamageMultiplier, "Damage scaling exceeded bounds.");
                previousHealth = health; previousDamage = damage;
            }
        });
        Run("boss waves occur exactly on interval and can be disabled", () =>
        {
            var rules = new WaveRules();
            for (int wave = 1; wave <= 100; wave++)
                Assert(rules.IsBossWave(wave) == (wave % 10 == 0), "Incorrect boss wave at " + wave);
            Assert(!rules.IsBossWave(0) && !rules.IsBossWave(-10), "Invalid waves must not trigger bosses.");
            rules.BossInterval = 0;
            Assert(!rules.IsBossWave(10), "Zero interval should disable boss waves.");
            rules.BossInterval = 4;
            Assert(rules.IsBossWave(4) && !rules.IsBossWave(5), "Custom boss interval ignored.");
        });
        Run("enemy types unlock only at their documented milestones", () =>
        {
            var rules = new WaveRules();
            var random = new Random(993151);
            for (int wave = 1; wave <= 12; wave++)
            {
                var seen = new HashSet<EnemyKind>();
                for (int sample = 0; sample < 3000; sample++) seen.Add(rules.ChooseEnemy(wave, random));
                Assert(seen.Contains(EnemyKind.Standard), "Every composition needs standard enemies.");
                Assert(seen.Contains(EnemyKind.Runner) == (wave >= 3), "Runner milestone violated.");
                Assert(seen.Contains(EnemyKind.Brute) == (wave >= 5), "Brute milestone violated.");
                Assert(seen.Contains(EnemyKind.Spitter) == (wave >= 7), "Spitter milestone violated.");
                Assert(seen.Contains(EnemyKind.Exploder) == (wave >= 7), "Exploder milestone violated.");
                Assert(!seen.Contains(EnemyKind.Boss), "Random composition must not spawn extra bosses.");
            }
        });
        Run("matching wave seeds reproduce exact compositions", () =>
        {
            var rules = new WaveRules();
            var first = new Random(87123);
            var second = new Random(87123);
            for (int sample = 0; sample < 10000; sample++)
                Assert(rules.ChooseEnemy(20, first) == rules.ChooseEnemy(20, second), "Seeded compositions diverged.");
        });
        Run("aggressive enemy weight tuning retains standard enemies", () =>
        {
            var rules = new WaveRules { RunnerWeight = 8, BruteWeight = 8, SpitterWeight = 8, ExploderWeight = 8 };
            var random = new Random(78151);
            int standardCount = 0;
            for (int i = 0; i < 100000; i++) if (rules.ChooseEnemy(20, random) == EnemyKind.Standard) standardCount++;
            Assert(standardCount > 36500 && standardCount < 39500, "Expected approximately 38% standard enemies after weight normalization.");
        });
        Run("wave configuration can disable special enemies", () =>
        {
            var rules = new WaveRules { RunnerWeight = 0, BruteWeight = 0, SpitterWeight = 0, ExploderWeight = 0 };
            var random = new Random(12);
            for (int i = 0; i < 1000; i++) Assert(rules.ChooseEnemy(100, random) == EnemyKind.Standard, "Zero weight type selected.");
        });
        Run("invalid count and growth configuration has safe lower bounds", () =>
        {
            var rules = new WaveRules { InitialCount = -3, ExtraPerWave = -5, MaximumCount = -1,
                HealthGrowth = -1, DamageGrowth = -1, MaximumHealthMultiplier = -2, MaximumDamageMultiplier = -2 };
            Assert(rules.GetEnemyCount(10) == 1, "At least one enemy is required.");
            Assert(rules.GetHealthMultiplier(10) == 1f && rules.GetDamageMultiplier(10) == 1f, "Stats fell below baseline.");
        });
        Run("enemy selection rejects an absent random source", () =>
        {
            bool threw = false;
            try { new WaveRules().ChooseEnemy(7, null); } catch (ArgumentNullException) { threw = true; }
            Assert(threw, "Missing random source should produce an explicit argument error.");
        });
    }
}
