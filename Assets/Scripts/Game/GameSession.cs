using UnityEngine;

namespace Zombio
{
    public enum SessionState { MainMenu, Playing, Paused, GameOver, Settings }

    /// <summary>Owns the run lifecycle and connects independent gameplay systems.</summary>
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }
        public SessionState State { get; private set; } = SessionState.MainMenu;
        public bool IsPlaying => State == SessionState.Playing;
        public int Score { get; private set; }
        public int Kills { get; private set; }
        public int BestScore { get; private set; }
        public int Seed = 1408;
        public float Sensitivity = 2f;
        public float MasterVolume = .65f;
        public WorldGenerator World { get; private set; }
        public PlayerController Player { get; private set; }
        public ThirdPersonCamera ViewCamera { get; private set; }
        public WeaponController Weapons { get; private set; }
        public WaveDirector Waves { get; private set; }
        public SurvivalHud Hud { get; private set; }
        public PickupDirector Pickups { get; private set; }
        GameTuning tuning;
        SessionState settingsReturn;
        ProceduralAudio sound;
        float lastKillTime = -100f;
        int streak;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Time.timeScale = 1f; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<GameSession>() == null)
                new GameObject("GameSession").AddComponent<GameSession>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            tuning = Resources.Load<GameTuning>("GameTuning");
            if (tuning == null) tuning = ScriptableObject.CreateInstance<GameTuning>();
            Sensitivity = PlayerPrefs.GetFloat("DeadSignal.Sensitivity", 2f);
            MasterVolume = PlayerPrefs.GetFloat("DeadSignal.Volume", .65f);
            BestScore = PlayerPrefs.GetInt("DeadSignal.Best", 0);
            Application.targetFrameRate = 120;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 65;
            QualitySettings.antiAliasing = 4;
            World = Child("Procedural District").AddComponent<WorldGenerator>();
            World.Generate(Seed);
            var playerObject = Child("Survivor");
            playerObject.transform.position = World.PlayerSpawn;
            playerObject.AddComponent<CharacterController>();
            Player = playerObject.AddComponent<PlayerController>();
            Player.MaxHealth = Mathf.Max(1, tuning.Health);
            Player.MaxStamina = Mathf.Max(1, tuning.Stamina);
            Player.WalkSpeed = tuning.WalkSpeed;
            Player.RunSpeed = tuning.RunSpeed;
            Player.SprintSpeed = tuning.SprintSpeed;
            Player.JumpHeight = tuning.JumpHeight;
            // Use a dedicated camera; a fresh Unity scene may contain a default one.
            foreach (var oldCamera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                oldCamera.enabled = false;
                var listener = oldCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
            var cameraObject = Child("Shoulder Camera");
            cameraObject.tag = "MainCamera";
            var lens = cameraObject.AddComponent<Camera>();
            lens.nearClipPlane = .08f;
            lens.farClipPlane = 180;
            lens.fieldOfView = 65;
            lens.clearFlags = CameraClearFlags.SolidColor;
            lens.backgroundColor = new Color(.055f, .095f, .12f);
            cameraObject.AddComponent<AudioListener>();
            ViewCamera = cameraObject.AddComponent<ThirdPersonCamera>();
            ViewCamera.Distance = tuning.CameraDistance;
            ViewCamera.ShoulderOffset = tuning.ShoulderOffset;
            ViewCamera.Configure(Player);
            Player.Configure(this, ViewCamera);
            Weapons = playerObject.AddComponent<WeaponController>();
            if (tuning.HasValidWeapons()) Weapons.Definitions = tuning.Weapons;
            Weapons.Configure(this, Player, ViewCamera);
            sound = Child("Synthesized Audio").AddComponent<ProceduralAudio>();
            sound.Initialize();
            Waves = Child("Wave Director").AddComponent<WaveDirector>();
            Waves.Rules = tuning.Waves ?? new WaveRules();
            Waves.MaximumAlive = tuning.MaximumAlive;
            Waves.IntermissionSeconds = tuning.Intermission;
            Waves.SpawnInterval = tuning.SpawnInterval;
            Waves.Configure(this);
            Pickups = Child("Pickups").AddComponent<PickupDirector>();
            Pickups.DropChance = tuning.PickupDropChance;
            Pickups.Configure(this);
            Hud = Child("Interface").AddComponent<SurvivalHud>();
            Hud.Configure(this);
            SetState(SessionState.MainMenu);
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--zombio-smoke") >= 0)
                gameObject.AddComponent<RuntimeSmokeChecks>();
        }

        GameObject Child(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child;
        }

        void Update()
        {
            AudioListener.volume = Mathf.Clamp01(MasterVolume);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (State == SessionState.Playing) Pause();
                else if (State == SessionState.Paused) Resume();
                else if (State == SessionState.Settings) CloseSettings();
            }
            if (IsPlaying && Input.GetKeyDown(KeyCode.Tab))
                Notify("SURVIVE / collect supplies / keep moving", new Color(.5f, 1f, .8f));
        }

        public void StartRun(int seed)
        {
            SetState(SessionState.MainMenu);
            Waves.Clear();
            ClearTransientEffects();
            Pickups.Clear();
            Seed = seed;
            World.Generate(seed);
            var body = Player.GetComponent<CharacterController>();
            body.enabled = false;
            Player.transform.SetPositionAndRotation(World.PlayerSpawn, Quaternion.identity);
            body.enabled = true;
            Player.ResetState();
            Weapons.ResetState();
            ViewCamera.Configure(Player);
            Score = 0;
            Kills = 0;
            streak = 0;
            lastKillTime = -100;
            Pickups.SeedSupplies();
            SetState(SessionState.Playing);
            Waves.Begin();
            Hud.Announce("SIGNAL LOST", "Stay alive. Make every round count.", new Color(.5f, 1f, .8f));
        }

        void ClearTransientEffects()
        {
            foreach (var effect in Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None))
            { effect.gameObject.SetActive(false); Destroy(effect.gameObject); }
        }

        void SetState(SessionState state)
        {
            State = state;
            Time.timeScale = state == SessionState.Playing ? 1f : 0f;
            Cursor.lockState = state == SessionState.Playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = state != SessionState.Playing;
        }
        public void Pause() { if (IsPlaying) SetState(SessionState.Paused); }
        public void Resume() { if (State == SessionState.Paused) SetState(SessionState.Playing); }
        public void GoToMenu()
        {
            Waves.Clear(); ClearTransientEffects(); Pickups.Clear(); SetState(SessionState.MainMenu);
        }
        public void OpenSettings() { settingsReturn = State; SetState(SessionState.Settings); }
        public void CloseSettings() { SaveSettings(); SetState(settingsReturn); }
        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("DeadSignal.Sensitivity", Sensitivity);
            PlayerPrefs.SetFloat("DeadSignal.Volume", MasterVolume);
            PlayerPrefs.Save();
        }
        public void Quit()
        {
            SaveSettings();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public void PlayerDied()
        {
            if (!IsPlaying) return;
            if (Score > BestScore)
            {
                BestScore = Score;
                PlayerPrefs.SetInt("DeadSignal.Best", BestScore);
                PlayerPrefs.Save();
            }
            SetState(SessionState.GameOver);
        }
        public void EnemyKilled(EnemyActor enemy, Vector3 position)
        {
            if (!IsPlaying) return;
            Kills++;
            streak = Time.time - lastKillTime <= 4f ? streak + 1 : 1;
            lastKillTime = Time.time;
            int reward = enemy.Kind == EnemyKind.Boss ? 1000 : enemy.Kind == EnemyKind.Brute ? 180 : 100;
            AddScore(reward + Mathf.Min(streak - 1, 10) * 20);
            if (streak == 3 || streak == 5 || streak % 10 == 0)
                Hud.Announce(streak + " KILL STREAK", "+" + Mathf.Min(streak - 1, 10) * 20 + " bonus per elimination", new Color(1f, .65f, .3f));
            HitConfirmed(true);
            PlaySound(SoundCue.Kill, position);
            Pickups.TryDrop(position, enemy.Kind == EnemyKind.Boss);
        }
        public void AddScore(int amount) { Score += Mathf.Max(0, amount); }
        public void Notify(string text, Color color) { if (Hud != null) Hud.Notify(text, color); }
        public void HitConfirmed(bool killed) { if (Hud != null) Hud.HitConfirmed(killed); }
        public void DamageFeedback(float amount)
        {
            if (Hud != null) Hud.DamageFeedback(amount);
            if (ViewCamera != null) ViewCamera.Shake(.2f);
        }
        public void PlaySound(SoundCue cue, Vector3 position) { if (sound != null) sound.Play(cue, position); }
        void OnApplicationFocus(bool focus) { if (!focus && IsPlaying) Pause(); }
        void OnDestroy()
        {
            if (Instance == this) { Instance = null; Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
    }
}
