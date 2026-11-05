using System.Collections.Generic;
using UnityEngine;

namespace Zombio
{
    public sealed class WaveDirector : MonoBehaviour
    {
        public WaveRules Rules = new WaveRules();
        [Min(1)] public int MaximumAlive = 22;
        [Min(1f)] public float IntermissionSeconds = 12f;
        [Min(0.2f)] public float SpawnInterval = 0.85f;
        [Min(5f)] public float MinimumSpawnDistance = 16f;

        public int WaveNumber { get; private set; }
        public int Remaining { get { return _pending + _living.Count; } }
        public int AliveCount { get { return _living.Count; } }
        public float Countdown { get; private set; }
        public bool InIntermission { get; private set; }
        public EnemyActor Boss { get; private set; }
        public Transform RuntimeRoot { get { return _runtimeRoot; } }

        private readonly List<EnemyActor> _living = new List<EnemyActor>();
        private GameSession _session;
        private System.Random _random;
        private Transform _runtimeRoot;
        private int _pending;
        private bool _running;
        private bool _bossPending;
        private float _nextSpawn;

        public void Configure(GameSession session) { _session = session; }

        public void Begin()
        {
            Clear();
            _runtimeRoot = new GameObject("Enemies and hostile effects").transform;
            _runtimeRoot.SetParent(transform, false);
            _random = new System.Random(_session.World.Seed ^ 0x2B97);
            _running = true;
            InIntermission = true;
            Countdown = 5f;
            _session.Notify("PERIMETER BREACH  /  PREPARE", new Color(0.48f, 0.92f, 0.85f));
        }

        public void Clear()
        {
            _running = false;
            if (_runtimeRoot != null)
            {
                _runtimeRoot.gameObject.SetActive(false);
                Destroy(_runtimeRoot.gameObject);
            }
            _runtimeRoot = null;
            _living.Clear();
            _pending = 0;
            _bossPending = false;
            Boss = null;
            WaveNumber = 0;
            Countdown = 0f;
            InIntermission = false;
        }

        private void Update()
        {
            if (!_running || _session == null || !_session.IsPlaying) return;
            if (InIntermission)
            {
                Countdown = Mathf.Max(0f, Countdown - Time.deltaTime);
                if (Countdown <= 0f) StartWave();
                return;
            }

            if (_pending > 0 && _living.Count < Mathf.Max(1, MaximumAlive) && Time.time >= _nextSpawn)
            {
                SpawnEnemy();
                _nextSpawn = Time.time + Mathf.Max(0.25f, SpawnInterval - WaveNumber * 0.012f);
            }
            if (_pending == 0 && _living.Count == 0)
            {
                InIntermission = true;
                Countdown = Mathf.Max(2f, IntermissionSeconds);
                _session.Notify("WAVE " + WaveNumber.ToString("00") + " CLEARED", new Color(0.46f, 1f, 0.73f));
                _session.Hud.Announce("DISTRICT SECURED", "Wave " + WaveNumber.ToString("00") + " cleared / resupply before the next breach", new Color(0.46f, 1f, 0.73f));
            }
        }

        private void StartWave()
        {
            WaveNumber++;
            InIntermission = false;
            _pending = Rules.GetEnemyCount(WaveNumber);
            _bossPending = Rules.IsBossWave(WaveNumber);
            _nextSpawn = Time.time;
            _session.PlaySound(SoundCue.Wave, _session.Player.transform.position);
            string text = "WAVE " + WaveNumber.ToString("00") + "  /  HOLD THE DISTRICT";
            if (WaveNumber == 3) text = "WAVE 03  /  RUNNERS INBOUND";
            if (WaveNumber == 5) text = "WAVE 05  /  ARMORED BRUTES INBOUND";
            if (WaveNumber == 7) text = "WAVE 07  /  BIOHAZARD CONTACTS";
            _session.Notify(text, new Color(1f, 0.72f, 0.35f));
            _session.Hud.Announce("WAVE " + WaveNumber.ToString("00"), text.Substring(text.IndexOf('/') + 1).Trim(), new Color(1f, 0.72f, 0.35f));
        }

        private void SpawnEnemy()
        {
            IReadOnlyList<Vector3> points = _session.World.SpawnPoints;
            if (points.Count == 0) return;
            int start = _random.Next(points.Count);
            Vector3 selected = points[start];
            float farthest = -1f;
            // Prefer a clear gate away from the player. If all are close, postpone spawning.
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 candidate = points[(start + i) % points.Count];
                float distance = (candidate - _session.Player.transform.position).sqrMagnitude;
                if (distance > farthest) { farthest = distance; selected = candidate; }
                if (distance >= MinimumSpawnDistance * MinimumSpawnDistance && !SpawnOccupied(candidate))
                { selected = candidate; break; }
            }
            if (farthest < 64f || SpawnOccupied(selected)) return;
            EnemyKind kind = _bossPending ? EnemyKind.Boss : Rules.ChooseEnemy(WaveNumber, _random);
            _bossPending = false;
            GameObject go = new GameObject(kind + " / wave " + WaveNumber);
            go.transform.SetParent(_runtimeRoot, false);
            go.transform.position = selected;
            EnemyActor actor = go.AddComponent<EnemyActor>();
            actor.Configure(_session, this, kind, Rules.GetHealthMultiplier(WaveNumber), Rules.GetDamageMultiplier(WaveNumber));
            _living.Add(actor);
            _pending--;
            if (kind == EnemyKind.Boss)
            {
                Boss = actor;
                _session.Notify("THE WARDEN  /  APEX CONTAMINATION", new Color(1f, 0.35f, 0.32f));
                _session.Hud.Announce("THE WARDEN", "APEX CONTAMINATION / leave the marked blast radius", new Color(1f, 0.35f, 0.32f));
                _session.ViewCamera.Shake(0.5f);
            }
        }

        private bool SpawnOccupied(Vector3 point)
        {
            for (int i = 0; i < _living.Count; i++)
                if ((_living[i].transform.position - point).sqrMagnitude < 5f) return true;
            return false;
        }

        public void OnEnemyDied(EnemyActor actor)
        {
            _living.Remove(actor);
            if (Boss == actor) Boss = null;
        }

        public Vector3 Separation(EnemyActor actor)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < _living.Count; i++)
            {
                if (_living[i] == actor) continue;
                Vector3 delta = actor.transform.position - _living[i].transform.position;
                delta.y = 0f;
                float sq = delta.sqrMagnitude;
                if (sq > 0.001f && sq < 2.6f) sum += delta.normalized * (1f - Mathf.Sqrt(sq) / 1.62f);
            }
            return Vector3.ClampMagnitude(sum, 1f);
        }
    }
}
