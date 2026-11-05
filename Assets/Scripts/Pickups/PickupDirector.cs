using System.Collections.Generic;
using UnityEngine;

namespace Zombio
{
    public enum PickupKind { Health, Ammo, Weapon, Damage, Speed, RapidFire, Score }

    public sealed class PickupDirector : MonoBehaviour
    {
        [Range(0, 1)] public float DropChance = .35f;
        public int MaximumPickups = 40;
        readonly List<PickupActor> active = new List<PickupActor>();
        GameSession session;
        System.Random random;
        float supplyTimer;
        public void Configure(GameSession owner) { session = owner; }
        public void Clear()
        {
            foreach (var item in active) if (item != null) { item.gameObject.SetActive(false); Destroy(item.gameObject); }
            active.Clear();
        }
        public void SeedSupplies()
        {
            random = new System.Random(session.Seed ^ 71413);
            supplyTimer = 35;
            int i = 0;
            foreach (Vector3 point in session.World.PickupPoints)
            {
                PickupKind kind = i == 0 || i == 4 ? PickupKind.Weapon : i % 3 == 0 ? PickupKind.Health : PickupKind.Ammo;
                Spawn(kind, point, i == 4 ? 2 : 1);
                i++;
            }
        }
        void Update()
        {
            if (session == null || !session.IsPlaying) return;
            active.RemoveAll(item => item == null);
            // Scavenging stays possible even if the player misses every shot.
            supplyTimer -= Time.deltaTime;
            if (supplyTimer <= 0)
            {
                supplyTimer = 35;
                if (session.World.PickupPoints.Count > 0)
                {
                    int i = random.Next(session.World.PickupPoints.Count);
                    Spawn(PickupKind.Ammo, session.World.PickupPoints[i]);
                }
            }
        }
        public void TryDrop(Vector3 position, bool boss)
        {
            if (random == null) return;
            if (!boss && random.NextDouble() > DropChance) return;
            position.y = 0;
            if (!session.World.IsWalkable(position)) return;
            var kind = boss ? PickupKind.Weapon : (PickupKind)random.Next(7);
            Spawn(kind, position, boss ? 2 : random.Next(1, 3));
        }
        void Spawn(PickupKind kind, Vector3 position, int weapon = 1)
        {
            active.RemoveAll(item => item == null);
            if (active.Count >= MaximumPickups) return;
            var item = new GameObject(kind + " Supply");
            item.transform.SetParent(transform, false);
            item.transform.position = position;
            var pickup = item.AddComponent<PickupActor>();
            pickup.Configure(session, kind, weapon);
            active.Add(pickup);
        }
    }
}
