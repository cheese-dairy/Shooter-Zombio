using System;

namespace Zombio
{
    public enum EnemyKind { Standard, Runner, Brute, Spitter, Exploder, Boss }

    /// <summary>Pure, deterministic wave tuning. No Unity dependency; safe to exercise in a console test.</summary>
    [Serializable]
    public sealed class WaveRules
    {
        public int InitialCount = 6;
        public int ExtraPerWave = 3;
        public int MaximumCount = 72;
        public int BossInterval = 10;
        public float HealthGrowth = 0.075f;
        public float DamageGrowth = 0.035f;
        public float MaximumHealthMultiplier = 4f;
        public float MaximumDamageMultiplier = 2.5f;
        public float RunnerWeight = 0.22f;
        public float BruteWeight = 0.15f;
        public float SpitterWeight = 0.13f;
        public float ExploderWeight = 0.12f;

        public int GetEnemyCount(int wave)
        {
            long count = Math.Max(1, InitialCount) + Math.Max(0L, (long)wave - 1L) * Math.Max(0, ExtraPerWave);
            return (int)Math.Min(Math.Max(1, MaximumCount), count);
        }

        public float GetHealthMultiplier(int wave)
        {
            return Math.Min(Math.Max(1f, MaximumHealthMultiplier), 1f + Math.Max(0L, (long)wave - 1L) * Math.Max(0f, HealthGrowth));
        }

        public float GetDamageMultiplier(int wave)
        {
            return Math.Min(Math.Max(1f, MaximumDamageMultiplier), 1f + Math.Max(0L, (long)wave - 1L) * Math.Max(0f, DamageGrowth));
        }

        public bool IsBossWave(int wave) { return wave > 0 && BossInterval > 0 && wave % BossInterval == 0; }

        public EnemyKind ChooseEnemy(int wave, Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (wave < 3) return EnemyKind.Standard;
            // Keep at least 38% of the composition as the readable, ordinary enemy.
            float runner = Math.Max(0f, RunnerWeight);
            float brute = wave >= 5 ? Math.Max(0f, BruteWeight) : 0f;
            float spitter = wave >= 7 ? Math.Max(0f, SpitterWeight) : 0f;
            float exploder = wave >= 7 ? Math.Max(0f, ExploderWeight) : 0f;
            float sum = runner + brute + spitter + exploder;
            float scale = sum > 0.62f ? 0.62f / sum : 1f;
            double roll = random.NextDouble();
            if (roll < runner * scale) return EnemyKind.Runner;
            if (roll < (runner + brute) * scale) return EnemyKind.Brute;
            if (roll < (runner + brute + spitter) * scale) return EnemyKind.Spitter;
            if (roll < sum * scale) return EnemyKind.Exploder;
            return EnemyKind.Standard;
        }
    }
}
