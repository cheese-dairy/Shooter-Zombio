using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zombio
{
    /// <summary>
    /// Builds a deterministic, navigable district using primitives. A connected road
    /// lattice is reserved before structures are placed; every collider footprint is
    /// represented in the same two-metre navigation grid. No baked assets are required.
    /// </summary>
    public sealed class WorldGenerator : MonoBehaviour
    {
        [Tooltip("The same seed produces the same architecture and supply locations.")]
        public int Seed = 73421;
        public const int GridSize = 43;
        public const float CellSize = 2f;
        public Vector3 PlayerSpawn { get; private set; }
        public IReadOnlyList<Vector3> SpawnPoints { get { return spawnPoints; } }
        public IReadOnlyList<Vector3> PickupPoints { get { return pickupPoints; } }
        public Transform GeometryRoot { get; private set; }

        private readonly List<Vector3> spawnPoints = new List<Vector3>();
        private readonly List<Vector3> pickupPoints = new List<Vector3>();
        private readonly List<GridCell> freeCells = new List<GridCell>();
        private readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<long, List<GridCell>> pathCache = new Dictionary<long, List<GridCell>>();
        private bool[,] cells;
        private GridNavigation navigation;
        private System.Random random;
        private Material asphalt, concrete, darkMetal, wallTeal, wallBlue, wallClay, amber, mint, coral, glass, vegetation;

        public void Generate(int seed)
        {
            ClearGeneratedWorld();
            Seed = seed;
            random = new System.Random(seed);
            cells = new bool[GridSize, GridSize];
            for (int x = 1; x < GridSize - 1; x++)
            for (int z = 1; z < GridSize - 1; z++) cells[x, z] = true;

            GeometryRoot = new GameObject("Generated District [seed " + seed + "]").transform;
            GeometryRoot.SetParent(transform, false);
            CreatePalette();
            Box("Asphalt foundation", new Vector3(0, -.3f, 0), new Vector3(86, .6f, 86), asphalt, true);
            BuildRoadMarkings();
            for (int bx = 0; bx < 4; bx++)
            for (int bz = 0; bz < 4; bz++) BuildBlock(4 + bx * 10, 4 + bz * 10, bx, bz);
            BuildBoundary();
            BuildAtmosphere();

            navigation = new GridNavigation(cells);
            for (int x = 1; x < GridSize - 1; x++)
            for (int z = 1; z < GridSize - 1; z++)
                if (cells[x, z]) freeCells.Add(new GridCell(x, z));
            PlayerSpawn = CellToWorld(new GridCell(21, 21));
            int[,] entrances = { { 2, 21 }, { 40, 21 }, { 21, 2 }, { 21, 40 }, { 2, 2 }, { 40, 40 }, { 2, 40 }, { 40, 2 } };
            for (int i = 0; i < entrances.GetLength(0); i++)
            {
                Vector3 p = CellToWorld(new GridCell(entrances[i, 0], entrances[i, 1]));
                spawnPoints.Add(p);
                Box("Quarantine entry marking", p + Vector3.up * .025f, new Vector3(3, .025f, 3), coral, false);
            }
            // Supplies sit in the connected road system, at least eight metres from the start.
            for (int i = 0; i < 24; i++)
            {
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    Vector3 p = CellToWorld(freeCells[random.Next(freeCells.Count)]);
                    if ((p - PlayerSpawn).sqrMagnitude < 64 || TooNearSupply(p)) continue;
                    pickupPoints.Add(p);
                    break;
                }
            }
            // Reliable initial supplies are also reachable before the opening wave.
            pickupPoints.Insert(0, CellToWorld(new GridCell(21, 24)));
            Physics.SyncTransforms();
        }

        public bool IsWalkable(Vector3 position)
        {
            return navigation != null && navigation.IsWalkable(WorldToCell(position));
        }

        public Vector3 RandomWalkablePoint()
        {
            return freeCells.Count == 0 ? PlayerSpawn : CellToWorld(freeCells[random.Next(freeCells.Count)]);
        }

        /// <summary>Request approximately twice per second per enemy, then steer toward the returned centre.</summary>
        public Vector3 GetWaypoint(Vector3 from, Vector3 goal)
        {
            if (navigation == null) return from;
            GridCell start = NearestWalkable(WorldToCell(from));
            GridCell target = NearestWalkable(WorldToCell(goal));
            long key = ((long)(start.Z * GridSize + start.X) << 32) | (uint)(target.Z * GridSize + target.X);
            List<GridCell> path;
            if (!pathCache.TryGetValue(key, out path))
            {
                path = navigation.FindPath(start, target);
                if (pathCache.Count >= 512) pathCache.Clear();
                pathCache.Add(key, path);
            }
            if (path.Count == 0) return from;
            if (path.Count == 1)
                return navigation.IsWalkable(WorldToCell(goal)) ? new Vector3(goal.x, 0, goal.z) : CellToWorld(target);
            return CellToWorld(path[1]);
        }

        private GridCell NearestWalkable(GridCell origin)
        {
            if (navigation.IsWalkable(origin)) return origin;
            GridCell best = new GridCell(21, 21);
            long distance = long.MaxValue;
            foreach (GridCell candidate in freeCells)
            {
                long dx = (long)candidate.X - origin.X;
                long dz = (long)candidate.Z - origin.Z;
                long d = dx * dx + dz * dz;
                if (d < distance) { best = candidate; distance = d; }
            }
            return best;
        }

        private bool TooNearSupply(Vector3 point)
        {
            foreach (Vector3 supply in pickupPoints) if ((supply - point).sqrMagnitude < 36f) return true;
            return false;
        }

        private static Vector3 CellToWorld(GridCell cell)
        {
            return new Vector3((cell.X - 21) * CellSize, 0, (cell.Z - 21) * CellSize);
        }

        private static GridCell WorldToCell(Vector3 point)
        {
            return new GridCell(Mathf.FloorToInt(point.x / CellSize + 21.5f), Mathf.FloorToInt(point.z / CellSize + 21.5f));
        }

        private void CreatePalette()
        {
            asphalt = MakeMaterial("Wet slate asphalt", new Color(.075f, .115f, .15f), .2f);
            concrete = MakeMaterial("Concrete", new Color(.25f, .31f, .32f));
            darkMetal = MakeMaterial("Charcoal metal", new Color(.075f, .12f, .16f), .35f);
            wallTeal = MakeMaterial("Oxidized teal", new Color(.16f, .37f, .36f));
            wallBlue = MakeMaterial("Storm blue", new Color(.19f, .28f, .39f));
            wallClay = MakeMaterial("Weathered terracotta", new Color(.43f, .27f, .25f));
            amber = MakeMaterial("Emergency amber", new Color(1f, .65f, .2f), 0, .6f);
            mint = MakeMaterial("Supply mint", new Color(.34f, .94f, .77f), 0, .5f);
            coral = MakeMaterial("Quarantine coral", new Color(.95f, .28f, .28f), 0, .3f);
            glass = MakeMaterial("Midnight glass", new Color(.055f, .16f, .19f), .65f);
            vegetation = MakeMaterial("Dusty foliage", new Color(.2f, .32f, .25f));
        }

        private Material MakeMaterial(string label, Color color, float smoothness = .05f, float emission = 0)
        {
            var material = new Material(Shader.Find("Standard"));
            material.name = label;
            material.color = color;
            material.SetFloat("_Glossiness", smoothness);
            if (emission > 0) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * emission); }
            materials.Add(material);
            return material;
        }

        private void BuildRoadMarkings()
        {
            // Paint never has a collider, so the controller crosses intersections smoothly.
            for (int road = 0; road < 3; road++)
            {
                float cross = (road - 1) * 20;
                for (int step = -9; step <= 9; step++)
                {
                    float along = step * 4;
                    Box("Lane stripe", new Vector3(cross, .018f, along), new Vector3(.15f, .016f, 1.8f), amber, false);
                    Box("Lane stripe", new Vector3(along, .018f, cross), new Vector3(1.8f, .016f, .15f), amber, false);
                }
            }
            for (int n = -3; n <= 3; n++)
            {
                Box("Extraction stripe", new Vector3(n * .75f, .03f, 3.5f), new Vector3(.4f, .025f, 1.5f), mint, false);
                Box("Extraction stripe", new Vector3(n * .75f, .03f, -3.5f), new Vector3(.4f, .025f, 1.5f), mint, false);
            }
        }

        private void BuildBlock(int x, int z, int bx, int bz)
        {
            Vector3 centre = CellToWorld(new GridCell(x + 2, z + 2));
            Box("Block paving", centre + Vector3.up * .035f, new Vector3(10, .05f, 10), concrete, false);
            int variant = random.Next(5);
            float height = 5 + random.Next(7);
            Material walls = random.Next(3) == 0 ? wallClay : random.Next(2) == 0 ? wallBlue : wallTeal;
            if (variant <= 1)
            {
                Building(x, z, 4, 5, height, walls, bx + bz * 4);
            }
            else if (variant == 2)
            {
                // A four-metre clear alley separates these two buildings.
                Building(x, z, 2, 5, height, walls, bx + bz * 4);
                Building(x + 4, z, 1, 5, height * .65f, wallBlue, -1);
            }
            else if (variant == 3)
            {
                Building(x, z, 5, 2, height, walls, bx + bz * 4);
                Cover(x, z + 4, 2, 1, "Supply containers", wallTeal, 1.4f);
                Cover(x + 4, z + 4, 1, 1, "Sealed rubble", concrete, .8f);
            }
            else
            {
                Building(x, z, 2, 3, height * .65f, walls, bx + bz * 4);
                Cover(x + 4, z, 1, 2, "Abandoned van", wallClay, 1.5f);
                Cover(x + 4, z + 4, 1, 1, "Planter", concrete, .65f);
                Vector3 plant = CellToWorld(new GridCell(x + 4, z + 4));
                Primitive("Tree trunk", PrimitiveType.Cylinder, plant + Vector3.up * 1.8f, new Vector3(.25f, 1.8f, .25f), darkMetal, false);
                Primitive("Angular tree canopy", PrimitiveType.Cube, plant + Vector3.up * 3.6f, new Vector3(1.9f, 1.7f, 1.9f), vegetation, false)
                    .transform.rotation = Quaternion.Euler(0, 25, 0);
            }
        }

        private void Building(int x, int z, int width, int depth, float height, Material walls, int number)
        {
            Occupy(x, z, width, depth);
            Vector3 centre = CellToWorld(new GridCell(x, z)) + new Vector3(width - 1, 0, depth - 1);
            float w = width * CellSize;
            float d = depth * CellSize;
            Box("Building shell", centre + Vector3.up * (height / 2), new Vector3(w, height, d), walls, true);
            Box("Dark plinth", centre + Vector3.up * .35f, new Vector3(w + .02f, .7f, d + .02f), darkMetal, false);
            Box("Roof cap", centre + Vector3.up * (height + .12f), new Vector3(w + .15f, .24f, d + .15f), darkMetal, false);
            Box("Roof machinery", centre + Vector3.up * (height + .65f), new Vector3(w * .45f, 1.1f, d * .3f), concrete, false);
            for (int floor = 0; floor < Mathf.FloorToInt(height / 2.3f); floor++)
            for (int window = 0; window < width; window++)
            {
                float xx = centre.x - w / 2 + 1 + window * 2;
                Material pane = random.Next(5) == 0 ? amber : glass;
                Box("Front window", new Vector3(xx, 1.65f + floor * 2.3f, centre.z - d / 2 - .018f), new Vector3(1.1f, .9f, .03f), pane, false);
                Box("Back window", new Vector3(xx, 1.65f + floor * 2.3f, centre.z + d / 2 + .018f), new Vector3(1.1f, .9f, .03f), glass, false);
            }
            if (number >= 0)
            {
                Box("Neon sign", centre + new Vector3(0, 3.2f, -d / 2 - .06f), new Vector3(Mathf.Min(w * .8f, 5), .55f, .06f), number % 3 == 0 ? coral : mint, false);
                // Closed entrances are part of the solid shell, avoiding unreachable interiors.
                Box("Sealed doorway", centre + new Vector3(0, 1, -d / 2 - .04f), new Vector3(1.2f, 2, .05f), darkMetal, false);
                Vector3 lamp = centre + new Vector3(w / 2 - .35f, 3.4f, -d / 2 + .25f);
                Primitive("Street lamp pole", PrimitiveType.Cylinder, new Vector3(lamp.x, 1.7f, lamp.z), new Vector3(.12f, 1.7f, .12f), darkMetal, false);
                Box("Lamp housing", lamp, new Vector3(.6f, .18f, .6f), amber, false);
            }
            if (number == 5)
            {
                // A skyline landmark that remains entirely inside its occupied footprint.
                Primitive("Water tower tank", PrimitiveType.Cylinder, centre + Vector3.up * (height + 2), new Vector3(w * .6f, 1.7f, w * .6f), wallTeal, false);
                Box("Tower beacon", centre + Vector3.up * (height + 3.85f), new Vector3(.6f, .4f, .6f), coral, false);
            }
        }

        private void Cover(int x, int z, int width, int depth, string name, Material material, float height)
        {
            Occupy(x, z, width, depth);
            Vector3 centre = CellToWorld(new GridCell(x, z)) + new Vector3(width - 1, height / 2, depth - 1);
            Box(name, centre, new Vector3(width * 2, height, depth * 2), material, true);
            Box(name + " lid", centre + Vector3.up * (height / 2 + .045f), new Vector3(width * 2, .09f, depth * 2), darkMetal, false);
            Box("Hazard strip", centre + new Vector3(0, .15f, -depth - .015f), new Vector3(width * 1.6f, .2f, .03f), amber, false);
        }

        private void Occupy(int x, int z, int width, int depth)
        {
            for (int xx = x; xx < x + width; xx++)
            for (int zz = z; zz < z + depth; zz++) cells[xx, zz] = false;
        }

        private void BuildBoundary()
        {
            // The outermost navigation cell is sealed by the physical quarantine wall.
            Box("North quarantine wall", new Vector3(0, 2.5f, 42), new Vector3(86, 5, 2), wallBlue, true);
            Box("South quarantine wall", new Vector3(0, 2.5f, -42), new Vector3(86, 5, 2), wallBlue, true);
            Box("East quarantine wall", new Vector3(42, 2.5f, 0), new Vector3(2, 5, 86), wallBlue, true);
            Box("West quarantine wall", new Vector3(-42, 2.5f, 0), new Vector3(2, 5, 86), wallBlue, true);
            for (int i = -4; i <= 4; i++)
            {
                float p = i * 9;
                Box("North perimeter warning", new Vector3(p, 3.7f, 40.98f), new Vector3(2, .2f, .03f), coral, false);
                Box("South perimeter warning", new Vector3(p, 3.7f, -40.98f), new Vector3(2, .2f, .03f), coral, false);
                Box("East perimeter warning", new Vector3(40.98f, 3.7f, p), new Vector3(.03f, .2f, 2), coral, false);
                Box("West perimeter warning", new Vector3(-40.98f, 3.7f, p), new Vector3(.03f, .2f, 2), coral, false);
            }
        }

        private void BuildAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .012f;
            RenderSettings.fogColor = new Color(.12f, .20f, .25f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.39f, .53f, .60f);
            RenderSettings.ambientEquatorColor = new Color(.22f, .30f, .32f);
            RenderSettings.ambientGroundColor = new Color(.13f, .16f, .20f);
            RenderSettings.ambientIntensity = .9f;
            QualitySettings.shadowDistance = 65;
            var sunObject = new GameObject("Late afternoon sun");
            sunObject.transform.SetParent(GeometryRoot, false);
            sunObject.transform.rotation = Quaternion.Euler(48, -30, 0);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1, .79f, .58f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .8f;
            RenderSettings.sun = sun;
        }

        private GameObject Box(string name, Vector3 position, Vector3 scale, Material material, bool collision)
        {
            return Primitive(name, PrimitiveType.Cube, position, scale, material, collision);
        }

        private GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collision)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(GeometryRoot, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision)
            {
                Collider collider = item.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
            }
            return item;
        }

        private void ClearGeneratedWorld()
        {
            if (GeometryRoot != null)
            {
                GeometryRoot.gameObject.SetActive(false);
                Destroy(GeometryRoot.gameObject);
            }
            foreach (Material material in materials) if (material != null) Destroy(material);
            materials.Clear();
            spawnPoints.Clear();
            pickupPoints.Clear();
            freeCells.Clear();
            pathCache.Clear();
            navigation = null;
        }

        private void OnDestroy()
        {
            foreach (Material material in materials) if (material != null) Destroy(material);
            materials.Clear();
        }
    }
}
