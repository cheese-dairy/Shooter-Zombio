using UnityEngine;

namespace Zombio
{
    [CreateAssetMenu(menuName = "Dead Signal/Game Tuning", fileName = "GameTuning")]
    public sealed class GameTuning : ScriptableObject
    {
        [Header("Survivor")]
        [Min(1)] public float Health = 100;
        [Min(1)] public float Stamina = 100;
        [Min(.1f)] public float WalkSpeed = 2.4f;
        [Min(.1f)] public float RunSpeed = 4.9f;
        [Min(.1f)] public float SprintSpeed = 8;
        [Min(.1f)] public float JumpHeight = 1.4f;
        [Header("Camera")]
        [Range(3.5f, 7.5f)] public float CameraDistance = 5;
        [Range(.25f, 1.2f)] public float ShoulderOffset = .6f;
        [Header("Survival pacing")]
        public WaveRules Waves = new WaveRules();
        [Min(1)] public int MaximumAlive = 22;
        [Min(2)] public float Intermission = 12;
        [Min(.25f)] public float SpawnInterval = .85f;
        [Range(0, 1)] public float PickupDropChance = .35f;
        [Header("Optional full weapon override; leave empty for the three shipped weapons")]
        public WeaponDefinition[] Weapons = new WeaponDefinition[0];

        public bool HasValidWeapons()
        {
            if (Weapons == null || Weapons.Length != 3) return false;
            foreach (var weapon in Weapons)
                if (weapon == null || string.IsNullOrEmpty(weapon.Name) || weapon.Magazine < 1 ||
                    weapon.StartingReserve < 0 || weapon.Pellets < 1 || weapon.ShotInterval <= 0 ||
                    weapon.ReloadSeconds <= 0 || weapon.Damage <= 0) return false;
            return true;
        }
    }
}
