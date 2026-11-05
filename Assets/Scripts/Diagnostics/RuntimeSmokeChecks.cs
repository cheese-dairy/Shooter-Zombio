using System;
using System.Collections;
using UnityEngine;

namespace Zombio
{
    /// <summary>Opt-in engine integration checks. Runs only with --zombio-smoke.</summary>
    public sealed class RuntimeSmokeChecks : MonoBehaviour
    {
        int assertions;
        bool failed;
        float started;
        void Awake()
        {
            started = Time.realtimeSinceStartup;
            Application.logMessageReceived += OnLog;
        }
        void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failed = true;
        }
        void Update()
        {
            if (failed || Time.realtimeSinceStartup - started > 90) Finish(false);
        }
        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("SMOKE FAIL: " + message);
            Debug.Log("SMOKE PASS: " + message);
        }
        IEnumerator Start()
        {
            yield return null;
            var game = GameSession.Instance;
            Check(game != null && game.State == SessionState.MainMenu, "Bootstrap enters main menu");
            foreach (int seed in new[] { 1408, 7, -921 })
            {
                game.StartRun(seed);
                yield return null;
                Check(game.World.IsWalkable(game.World.PlayerSpawn), "Player spawn walkable: " + seed);
                Check(game.World.SpawnPoints.Count == 8, "Eight spawn gates: " + seed);
                foreach (var point in game.World.SpawnPoints)
                    Check(game.World.IsWalkable(point), "Walkable enemy spawn");
                foreach (var point in game.World.PickupPoints)
                {
                    Check(game.World.IsWalkable(point), "Walkable pickup");
                    Vector3 cursor = point;
                    int steps = 0;
                    while ((cursor - game.World.PlayerSpawn).sqrMagnitude > .1f && steps++ < 200)
                    {
                        Vector3 next = game.World.GetWaypoint(cursor, game.World.PlayerSpawn);
                        Check((next - cursor).sqrMagnitude > .001f, "Supply path advances");
                        cursor = next;
                    }
                    Check(steps < 200, "Supply connects to player");
                }
                Check(Physics.Raycast(game.World.PlayerSpawn + Vector3.up, Vector3.down, 3), "Physical floor exists");
            }
            // Real camera/muzzle raycasts, then cover with the muzzle inside its collider.
            var dummyObject = new GameObject("Smoke combat target");
            dummyObject.transform.position = new Vector3(0, 0, 7);
            var dummy = dummyObject.AddComponent<EnemyActor>();
            dummy.Configure(game, game.Waves, EnemyKind.Standard, 1, 1);
            game.ViewCamera.transform.SetPositionAndRotation(new Vector3(0, 1.3f, -5), Quaternion.identity);
            Physics.SyncTransforms();
            int bullets = game.Weapons.Ammo;
            Check(game.Weapons.TryFire(), "Weapon fires on demand");
            Check(game.Weapons.Ammo == bullets - 1 && dummy.Health < dummy.MaxHealth, "Camera/muzzle rays hit target and spend ammo");
            float dummyHealth = dummy.Health;
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.transform.position = new Vector3(0, 1, .85f);
            cover.transform.localScale = new Vector3(3, 2, 1.1f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.3f);
            game.ViewCamera.transform.SetPositionAndRotation(new Vector3(0, 1.3f, -5), Quaternion.identity);
            Check(game.Weapons.TryFire(), "Weapon cooldown expires");
            Check(dummy.Health == dummyHealth, "Cover blocks muzzle even when barrel penetrates it");
            cover.SetActive(false);
            Destroy(cover);
            dummy.TakeDamage(10000, dummy.transform.position);
            game.Weapons.BeginReload();
            Check(game.Weapons.IsReloading, "Reload begins");
            yield return new WaitForSeconds(1.5f);
            Check(!game.Weapons.IsReloading && game.Weapons.Ammo == game.Weapons.MagazineSize, "Reload completes with reserve ammo");
            game.Player.TakeDamage(25);
            Check(Mathf.Approximately(game.Player.Health, 75), "Damage removes health");
            game.Player.Heal(15);
            Check(Mathf.Approximately(game.Player.Health, 90), "Health pickup API restores health");
            game.Weapons.UnlockWeapon(1);
            game.Weapons.UnlockWeapon(2);
            Check(game.Weapons.IsUnlocked(1) && game.Weapons.IsUnlocked(2), "Weapon pickup APIs unlock both weapons");
            game.Weapons.SelectWeapon(1);
            Check(game.Weapons.WeaponIndex == 1, "Weapon selection changes active weapon");
            game.Pause();
            float health = game.Player.Health;
            game.Player.TakeDamage(50);
            Check(game.State == SessionState.Paused && Time.timeScale == 0 && game.Player.Health == health, "Pause blocks damage and freezes simulation");
            game.Resume();
            Check(game.IsPlaying, "Resume returns to playing");
            // Keep normal wave count, but shorten intermission/spawn delays for this opt-in check.
            game.Waves.IntermissionSeconds = 2;
            game.Waves.SpawnInterval = .25f;
            var kinds = new System.Collections.Generic.HashSet<EnemyKind>();
            float deadline = Time.realtimeSinceStartup + 65;
            while (game.Waves.WaveNumber < 11 && Time.realtimeSinceStartup < deadline)
            {
                Time.timeScale = 20;
                foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None))
                {
                    kinds.Add(enemy.Kind);
                    enemy.TakeDamage(100000, enemy.transform.position);
                }
                game.Player.Heal(100);
                yield return null;
            }
            Check(game.Waves.WaveNumber >= 11, "Ten waves fully clear, including boss wave");
            Check(kinds.Contains(EnemyKind.Boss), "Boss spawned through the director");
            Check(game.Kills > 100 && game.Score > 10000, "Kills, score and streak rewards accumulate");
            // Instantiate every archetype explicitly as composition is probabilistic.
            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            {
                var go = new GameObject("Smoke " + kind);
                go.transform.position = game.World.SpawnPoints[0];
                var enemy = go.AddComponent<EnemyActor>();
                enemy.Configure(game, game.Waves, kind, 1, 1);
                Check(enemy.GetComponent<CharacterController>() != null && enemy.Health > 0, "Configured collider and health: " + kind);
                enemy.TakeDamage(100000, go.transform.position);
                Check(enemy.IsDead, "Damage/death: " + kind);
            }
            game.Player.TakeDamage(10000);
            Check(game.State == SessionState.GameOver, "Lethal damage enters game over");
            game.StartRun(1408);
            yield return null;
            Check(game.Kills == 0 && game.Score == 0 && game.Player.Health == game.Player.MaxHealth, "Restart resets run state");
            Check(game.Waves.AliveCount == 0 && !game.Weapons.IsUnlocked(1), "Restart clears enemies and inventory unlocks");
            Finish(!failed);
        }
        void Finish(bool success)
        {
            enabled = false;
            StopAllCoroutines();
            Application.logMessageReceived -= OnLog;
            Debug.Log("DEAD SIGNAL SMOKE " + (success ? "PASS" : "FAIL") + " / assertions=" + assertions);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(success ? 0 : 1);
#else
            Application.Quit(success ? 0 : 1);
#endif
        }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
