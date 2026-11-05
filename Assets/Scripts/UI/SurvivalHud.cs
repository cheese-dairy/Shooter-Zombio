using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombio
{
    /// <summary>
    /// Asset-free, resolution-independent HUD and menus. GameSession creates and wires this component.
    /// The interface uses unscaled time so pause, settings, and death transitions remain animated.
    /// </summary>
    public sealed class SurvivalHud : MonoBehaviour
    {
        const float Width = 1600f;
        const float Height = 900f;
        static readonly Color Ink = new Color(0.035f, 0.065f, 0.075f);
        static readonly Color Panel = new Color(0.045f, 0.085f, 0.092f, 0.94f);
        static readonly Color Cream = new Color(0.94f, 0.95f, 0.88f);
        static readonly Color Muted = new Color(0.53f, 0.66f, 0.65f);
        static readonly Color Mint = new Color(0.43f, 0.98f, 0.76f);
        static readonly Color Orange = new Color(1f, 0.55f, 0.28f);
        static readonly Color Red = new Color(1f, 0.26f, 0.26f);

        sealed class Toast
        {
            public string Text;
            public Color Color;
            public float Born;
        }

        readonly List<Toast> toasts = new List<Toast>();
        readonly Dictionary<int, GUIStyle> labels = new Dictionary<int, GUIStyle>();
        GameSession session;
        GUIStyle buttonStyle;
        GUIStyle inputStyle;
        GUIStyle sliderStyle;
        GUIStyle thumbStyle;
        SessionState previousState;
        string seedText = "104729";
        string announcementTitle = "";
        string announcementSubtitle = "";
        Color announcementColor = Mint;
        float announcementAt = -100f;
        float damageAt = -100f;
        float damageStrength;
        float hitAt = -100f;
        bool killHit;
        float shownHealth = 1f;
        float shownStamina = 1f;
        float stateChangedAt;
        int lastScore;
        float scoreAt = -100f;

        public void Configure(GameSession owner)
        {
            session = owner;
            seedText = owner.Seed.ToString();
            previousState = owner.State;
            stateChangedAt = Time.unscaledTime;
        }

        public void Notify(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            toasts.Add(new Toast { Text = text, Color = color, Born = Time.unscaledTime });
            if (toasts.Count > 5) toasts.RemoveAt(0);
        }

        public void HitConfirmed(bool killed)
        {
            hitAt = Time.unscaledTime;
            killHit = killed;
        }

        public void DamageFeedback(float amount)
        {
            damageAt = Time.unscaledTime;
            damageStrength = Mathf.Clamp(amount / 35f, 0.35f, 1f);
        }

        public void Announce(string title, string subtitle, Color color)
        {
            announcementTitle = title ?? "";
            announcementSubtitle = subtitle ?? "";
            announcementColor = color;
            announcementAt = Time.unscaledTime;
        }

        void Update()
        {
            if (session == null) return;
            if (previousState != session.State)
            {
                SessionState oldState = previousState;
                previousState = session.State;
                stateChangedAt = Time.unscaledTime;
                if (session.State == SessionState.Playing &&
                    (oldState == SessionState.MainMenu || oldState == SessionState.GameOver))
                {
                    toasts.Clear();
                    shownHealth = 1f;
                    shownStamina = 1f;
                    damageAt = hitAt = -100f;
                }
            }
            if (session.Player != null)
            {
                float smoothing = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
                shownHealth = Mathf.Lerp(shownHealth,
                    session.Player.Health / Mathf.Max(1f, session.Player.MaxHealth), smoothing);
                shownStamina = Mathf.Lerp(shownStamina,
                    session.Player.Stamina / Mathf.Max(1f, session.Player.MaxStamina), smoothing);
            }
            if (lastScore != session.Score)
            {
                lastScore = session.Score;
                scoreAt = Time.unscaledTime;
            }
            toasts.RemoveAll(item => Time.unscaledTime - item.Born > 4.5f);
        }

        void EnsureStyles()
        {
            if (buttonStyle != null) return;
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(24, 20, 0, 0),
                border = new RectOffset(0, 0, 0, 0)
            };
            SetState(buttonStyle.normal, Cream);
            SetState(buttonStyle.hover, Mint);
            SetState(buttonStyle.active, Ink);
            SetState(buttonStyle.focused, Mint);
            inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 24, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(16, 12, 4, 4),
                border = new RectOffset(0, 0, 0, 0)
            };
            SetState(inputStyle.normal, Cream);
            SetState(inputStyle.focused, Mint);
            SetState(inputStyle.hover, Cream);
            SetState(inputStyle.active, Cream);
            sliderStyle = new GUIStyle(GUI.skin.horizontalSlider)
            {
                fixedHeight = 8, margin = new RectOffset(0, 0, 14, 14),
                border = new RectOffset(0, 0, 0, 0)
            };
            sliderStyle.normal.background = Texture2D.whiteTexture;
            thumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb)
            {
                fixedWidth = 14, fixedHeight = 24,
                margin = new RectOffset(0, 0, -8, 0),
                border = new RectOffset(0, 0, 0, 0)
            };
            thumbStyle.normal.background = Texture2D.whiteTexture;
            thumbStyle.hover.background = Texture2D.whiteTexture;
            thumbStyle.active.background = Texture2D.whiteTexture;
        }

        static void SetState(GUIStyleState state, Color textColor)
        {
            state.background = null;
            state.textColor = textColor;
        }

        void OnGUI()
        {
            if (session == null) return;
            EnsureStyles();
            Matrix4x4 originalMatrix = GUI.matrix;
            Color originalColor = GUI.color;
            Color originalBackground = GUI.backgroundColor;
            Color originalContent = GUI.contentColor;
            int originalDepth = GUI.depth;
            bool originalEnabled = GUI.enabled;
            try
            {
                GUI.depth = -20;
                GUI.color = GUI.contentColor = Color.white;
                float scale = Mathf.Min(Screen.width / Width, Screen.height / Height);
                Vector3 offset = new Vector3((Screen.width - Width * scale) * 0.5f,
                    (Screen.height - Height * scale) * 0.5f, 0f);

                // Full-screen shading also covers ultrawide or portrait margins.
                if (session.State != SessionState.Playing)
                {
                    GUI.matrix = Matrix4x4.identity;
                    Fill(new Rect(0, 0, Screen.width, Screen.height),
                        new Color(0.015f, 0.035f, 0.04f, session.State == SessionState.MainMenu ? 0.45f : 0.73f));
                }
                GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1));
                if (session.State == SessionState.Playing || session.State == SessionState.Paused ||
                    session.State == SessionState.GameOver)
                    DrawHud();
                switch (session.State)
                {
                    case SessionState.MainMenu: DrawMainMenu(); break;
                    case SessionState.Paused: DrawPauseMenu(); break;
                    case SessionState.GameOver: DrawGameOver(); break;
                    case SessionState.Settings: DrawSettings(); break;
                }
                if (session.State == SessionState.Playing)
                {
                    DrawToasts();
                    DrawAnnouncement();
                }
            }
            finally
            {
                GUI.matrix = originalMatrix;
                GUI.color = originalColor;
                GUI.backgroundColor = originalBackground;
                GUI.contentColor = originalContent;
                GUI.enabled = originalEnabled;
                GUI.depth = originalDepth;
            }
        }

        void DrawMainMenu()
        {
            Fill(new Rect(0, 0, 760, Height), new Color(Ink.r, Ink.g, Ink.b, 0.91f));
            Fill(new Rect(760, 0, 2, Height), new Color(Mint.r, Mint.g, Mint.b, 0.22f));
            for (int i = 0; i < 16; i++)
                Fill(new Rect(763 + i * 12, 0, 12, Height), new Color(Ink.r, Ink.g, Ink.b, 0.32f * (1 - i / 16f)));
            DrawTelemetry(66, 50, "QUARANTINE ZONE / SIGNAL 06", Mint);
            float enter = Ease(Mathf.Clamp01((Time.unscaledTime - stateChangedAt) * 2f));
            float titleX = 66 - 26 * (1 - enter);
            Label(new Rect(titleX, 134, 690, 119), "DEAD", 116, Cream, FontStyle.Bold);
            Label(new Rect(titleX - 3, 237, 690, 119), "SIGNAL", 116, Mint, FontStyle.Bold);
            Fill(new Rect(70, 375, 63, 4), Orange);
            Label(new Rect(70, 403, 570, 30), "THE CITY WENT QUIET. IT DIDN'T STAY EMPTY.", 17, Cream, FontStyle.Bold);
            Label(new Rect(70, 444, 590, 65),
                "Scavenge the district. Hold the line.\nSurvive an endless transmission of the infected.", 19, Muted);

            Label(new Rect(70, 540, 300, 25), "DISTRICT SEED", 13, Muted, FontStyle.Bold);
            Fill(new Rect(70, 572, 325, 52), new Color(1, 1, 1, 0.055f));
            seedText = GUI.TextField(new Rect(70, 572, 325, 52), seedText, 11, inputStyle);
            if (Button(new Rect(409, 572, 229, 52), "RANDOMIZE", false))
                seedText = new System.Random(Environment.TickCount).Next(1, int.MaxValue).ToString();
            bool validSeed = int.TryParse(seedText, out int parsedSeed);
            if (!validSeed)
                Label(new Rect(70, 629, 560, 20), "Enter a whole number between -2147483648 and 2147483647.", 12, Orange);
            GUI.enabled = validSeed;
            if (Button(new Rect(70, 662, 568, 66), "DEPLOY INTO THE DISTRICT     /     START", true))
                InvokeAndExit(() => session.StartRun(parsedSeed));
            GUI.enabled = true;
            if (Button(new Rect(70, 746, 276, 53), "SETTINGS", false))
                InvokeAndExit(session.OpenSettings);
            if (Button(new Rect(362, 746, 276, 53), "QUIT", false))
                InvokeAndExit(session.Quit);
            Label(new Rect(70, 836, 600, 24), "ENDLESS SURVIVAL     /     BEST SIGNAL  " + session.BestScore.ToString("N0"), 13, Muted);

            float cardY = 605 + Mathf.Sin(Time.unscaledTime * 0.5f) * 3;
            Card(new Rect(1060, cardY, 460, 216), Mint);
            Label(new Rect(1088, cardY + 24, 400, 24), "FIELD MANUAL   /   01", 14, Mint, FontStyle.Bold);
            Label(new Rect(1088, cardY + 61, 400, 35), "KEEP MOVING.", 31, Cream, FontStyle.Bold);
            Label(new Rect(1088, cardY + 109, 400, 74),
                "WASD move  ·  Shift sprint  ·  Space jump\nRMB aim  ·  LMB fire  ·  R reload\nCtrl crouch  ·  C slide  ·  Q dodge", 16, Muted);
            Label(new Rect(1080, 54, 440, 24), "PROCEDURAL DISTRICT   /   NO SAFE ROUTE", 13, Cream,
                FontStyle.Normal, TextAnchor.MiddleRight);
        }

        void DrawHud()
        {
            if (session.Player == null || session.Weapons == null || session.Waves == null) return;
            var player = session.Player;
            var weapon = session.Weapons;
            var waves = session.Waves;
            Card(new Rect(42, 36, 303, 100), Mint);
            Label(new Rect(63, 49, 220, 22), "SURVIVAL TRANSMISSION", 12, Muted, FontStyle.Bold);
            Label(new Rect(61, 74, 190, 47), "WAVE " + waves.WaveNumber.ToString("00"), 34, Cream, FontStyle.Bold);
            Label(new Rect(245, 80, 80, 24), waves.InIntermission ? "CLEAR" : waves.Remaining.ToString("00"),
                23, waves.InIntermission ? Mint : Orange, FontStyle.Bold, TextAnchor.MiddleRight);
            Label(new Rect(215, 110, 110, 16), waves.InIntermission ? "RESUPPLY" : "REMAINING",
                10, Muted, FontStyle.Bold, TextAnchor.MiddleRight);

            Card(new Rect(1240, 36, 318, 100), Mint);
            float scoreFlash = Mathf.Clamp01(1 - (Time.unscaledTime - scoreAt) * 2f);
            Label(new Rect(1263, 48, 270, 23), "SIGNAL SCORE", 12, Muted, FontStyle.Bold,
                TextAnchor.MiddleRight);
            Label(new Rect(1263, 74, 270, 44), session.Score.ToString("N0"), 34,
                Color.Lerp(Cream, Mint, scoreFlash), FontStyle.Bold, TextAnchor.MiddleRight);
            Label(new Rect(1240, 143, 318, 22), "ELIMINATIONS   " + session.Kills.ToString("000"),
                14, Cream, FontStyle.Bold, TextAnchor.MiddleRight);

            bool critical = player.Health <= player.MaxHealth * 0.25f;
            float pulse = critical ? 0.65f + Mathf.Sin(Time.unscaledTime * 7f) * 0.35f : 1f;
            Card(new Rect(42, 735, 365, 121), critical ? Red : Mint);
            Label(new Rect(62, 750, 200, 25), critical ? "VITALS / CRITICAL" : "VITALS / STABLE", 13,
                critical ? Alpha(Red, pulse) : Muted, FontStyle.Bold);
            Label(new Rect(294, 743, 92, 33), Mathf.CeilToInt(player.Health).ToString("000"), 26,
                Cream, FontStyle.Bold, TextAnchor.MiddleRight);
            Bar(new Rect(62, 789, 324, 12), shownHealth, critical ? Red : Mint);
            Label(new Rect(62, 818, 103, 17), "STAMINA", 10, Muted, FontStyle.Bold);
            Bar(new Rect(151, 824, 235, 4), shownStamina, Orange);

            Card(new Rect(1193, 724, 365, 132), Orange);
            Label(new Rect(1213, 739, 320, 26), weapon.WeaponName.ToUpperInvariant(), 17, Cream,
                FontStyle.Bold, TextAnchor.MiddleRight);
            Label(new Rect(1213, 766, 237, 64), weapon.Ammo.ToString("00"), 54,
                weapon.Ammo == 0 ? Red : Cream, FontStyle.Bold, TextAnchor.MiddleRight);
            Label(new Rect(1460, 792, 73, 29), "/ " + weapon.ReserveAmmo.ToString("000"), 20,
                Muted, FontStyle.Normal, TextAnchor.MiddleRight);
            Label(new Rect(1214, 831, 319, 16), weapon.IsReloading ? "RELOAD IN PROGRESS" : "1 / SIDEARM    2 / RIFLE    3 / SHOTGUN",
                10, weapon.IsReloading ? Orange : Muted, FontStyle.Bold, TextAnchor.MiddleRight);
            if (weapon.IsReloading)
                Bar(new Rect(1193, 852, 365, 4), weapon.ReloadProgress, Orange);

            if (session.State == SessionState.Playing)
            {
                DrawCrosshair(weapon.IsReloading, weapon.ReloadProgress);
                DrawDamage();
            }
            Label(new Rect(452, 846, 696, 21), "SHIFT SPRINT   ·   CTRL CROUCH   ·   C SLIDE   ·   Q DODGE   ·   ESC PAUSE", 11,
                Alpha(Cream, 0.65f), FontStyle.Normal, TextAnchor.MiddleCenter);
            if (waves.InIntermission)
            {
                Card(new Rect(627, 37, 346, 66), Mint);
                Label(new Rect(645, 44, 310, 19), "NEXT TRANSMISSION IN", 11, Muted,
                    FontStyle.Bold, TextAnchor.MiddleCenter);
                Label(new Rect(645, 65, 310, 30), Mathf.CeilToInt(waves.Countdown).ToString("00") + " SEC",
                    23, Mint, FontStyle.Bold, TextAnchor.MiddleCenter);
            }
            if (waves.Boss != null && !waves.Boss.IsDead)
            {
                Card(new Rect(545, 116, 510, 67), Red);
                Label(new Rect(562, 124, 475, 23), "THE WARDEN  /  HEAVY CONTACT", 14, Cream,
                    FontStyle.Bold, TextAnchor.MiddleCenter);
                Bar(new Rect(565, 158, 470, 8), waves.Boss.Health / Mathf.Max(1, waves.Boss.MaxHealth), Red);
            }
        }

        void DrawCrosshair(bool reloading, float reloadProgress)
        {
            Vector2 centre = new Vector2(800, 450);
            float age = Time.unscaledTime - hitAt;
            float hit = Mathf.Clamp01(1 - age / 0.24f);
            Color color = hit > 0 ? (killHit ? Orange : Mint) : Alpha(Cream, 0.9f);
            if (reloading)
            {
                for (int i = 0; i < 28; i++)
                {
                    float a = (i / 28f * 360 - 90) * Mathf.Deg2Rad;
                    float b = (i / 28f * 360 - 82) * Mathf.Deg2Rad;
                    Vector2 p = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 23;
                    Vector2 q = centre + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 23;
                    Line(p, q, 3, i / 28f < reloadProgress ? Orange : Alpha(Cream, 0.15f));
                }
                Label(new Rect(710, 489, 180, 24), "RELOADING", 11, Orange, FontStyle.Bold, TextAnchor.MiddleCenter);
                return;
            }
            float gap = 7f + hit * 4f;
            Fill(new Rect(799, 449, 2, 2), color);
            Fill(new Rect(799, 450 - gap - 7, 2, 7), color);
            Fill(new Rect(799, 450 + gap, 2, 7), color);
            Fill(new Rect(800 - gap - 7, 449, 7, 2), color);
            Fill(new Rect(800 + gap, 449, 7, 2), color);
            if (hit > 0)
            {
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        Line(centre + new Vector2(x, y) * 13, centre + new Vector2(x, y) * 20, 2,
                            Alpha(color, hit));
                if (killHit)
                    Label(new Rect(680, 493, 240, 21), "CONFIRMED", 11, Alpha(Orange, hit), FontStyle.Bold,
                        TextAnchor.MiddleCenter);
            }
        }

        void DrawDamage()
        {
            float flash = Mathf.Clamp01(1 - (Time.unscaledTime - damageAt) / 0.65f) * damageStrength;
            if (flash <= 0) return;
            for (int i = 0; i < 18; i++)
            {
                float falloff = 1f - i / 18f;
                float alpha = flash * 0.45f * falloff * falloff;
                float thickness = 6;
                Color red = Alpha(Red, alpha);
                Fill(new Rect(i * thickness, 0, thickness, Height), red);
                Fill(new Rect(Width - (i + 1) * thickness, 0, thickness, Height), red);
                Fill(new Rect(0, i * thickness, Width, thickness), red);
                Fill(new Rect(0, Height - (i + 1) * thickness, Width, thickness), red);
            }
            Fill(new Rect(0, 0, Width, Height), Alpha(Red, flash * 0.035f));
        }

        void DrawToasts()
        {
            for (int i = 0; i < toasts.Count; i++)
            {
                Toast toast = toasts[toasts.Count - 1 - i];
                float age = Time.unscaledTime - toast.Born;
                float alpha = Mathf.Min(Mathf.Clamp01(age * 7f), Mathf.Clamp01((4.5f - age) * 2f));
                float slide = (1 - Ease(Mathf.Clamp01(age * 4f))) * 40;
                float y = 660 - i * 49;
                Fill(new Rect(42 - slide, y, 435, 39), Alpha(Panel, alpha * 0.92f));
                Fill(new Rect(42 - slide, y, 3, 39), Alpha(toast.Color, alpha));
                Label(new Rect(58 - slide, y + 3, 405, 32), toast.Text, 15, Alpha(Cream, alpha));
            }
        }

        void DrawAnnouncement()
        {
            float age = Time.unscaledTime - announcementAt;
            if (age < 0 || age > 4f) return;
            float alpha = Mathf.Min(Mathf.Clamp01(age * 3.5f), Mathf.Clamp01((4f - age) * 1.7f));
            float y = 223 - (1 - Ease(Mathf.Clamp01(age * 3))) * 25;
            Fill(new Rect(460, y, 680, 109), Alpha(Ink, alpha * 0.88f));
            Fill(new Rect(460, y, 680 * Ease(Mathf.Clamp01(age * 1.8f)), 2), Alpha(announcementColor, alpha));
            Label(new Rect(475, y + 14, 650, 49), announcementTitle, 36, Alpha(announcementColor, alpha),
                FontStyle.Bold, TextAnchor.MiddleCenter);
            Label(new Rect(478, y + 66, 644, 27), announcementSubtitle, 15, Alpha(Cream, alpha),
                FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        void DrawPauseMenu()
        {
            Rect frame = new Rect(506, 163, 588, 575);
            Card(frame, Mint);
            DrawTelemetry(546, 193, "TRANSMISSION ON HOLD", Mint);
            Label(new Rect(546, 239, 510, 82), "PAUSED", 66, Cream, FontStyle.Bold);
            Label(new Rect(548, 331, 500, 27), "Take a breath. The district can wait.", 18, Muted);
            if (Button(new Rect(546, 394, 508, 65), "RESUME TRANSMISSION", true)) InvokeAndExit(session.Resume);
            if (Button(new Rect(546, 478, 508, 57), "SETTINGS", false)) InvokeAndExit(session.OpenSettings);
            if (Button(new Rect(546, 551, 508, 57), "RETURN TO MAIN MENU", false)) InvokeAndExit(session.GoToMenu);
            Label(new Rect(546, 646, 508, 27), "WAVE " + session.Waves.WaveNumber.ToString("00") +
                "    /    " + session.Score.ToString("N0") + " POINTS    /    ESC TO RESUME", 13, Muted,
                FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        void DrawGameOver()
        {
            float enter = Ease(Mathf.Clamp01((Time.unscaledTime - stateChangedAt) * 1.5f));
            float y = 139 + (1 - enter) * 30;
            Card(new Rect(438, y, 724, 625), Orange);
            DrawTelemetry(482, y + 31, "CONNECTION LOST / RUN TERMINATED", Orange);
            Label(new Rect(478, y + 78, 640, 87), "SIGNAL LOST", 68, Cream, FontStyle.Bold);
            Label(new Rect(484, y + 170, 630, 31), "The district remembers every survivor.", 19, Muted);
            Stat(new Rect(484, y + 235, 194, 107), "FINAL SCORE", session.Score.ToString("N0"), Mint);
            Stat(new Rect(700, y + 235, 182, 107), "WAVE REACHED", session.Waves.WaveNumber.ToString("00"), Orange);
            Stat(new Rect(904, y + 235, 210, 107), "ELIMINATIONS", session.Kills.ToString("000"), Cream);
            Label(new Rect(484, y + 358, 630, 26), "PERSONAL BEST  /  " + session.BestScore.ToString("N0"),
                14, Muted, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (Button(new Rect(484, y + 410, 630, 65), "REDEPLOY / SAME DISTRICT", true))
                InvokeAndExit(() => session.StartRun(session.Seed));
            if (Button(new Rect(484, y + 492, 630, 57), "MAIN MENU / NEW DISTRICT", false))
                InvokeAndExit(session.GoToMenu);
            Label(new Rect(484, y + 572, 630, 24), "Move with intent. Reload before the silence ends.",
                14, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        void DrawSettings()
        {
            Card(new Rect(397, 87, 806, 726), Mint);
            DrawTelemetry(443, 118, "FIELD CONFIGURATION", Mint);
            Label(new Rect(440, 156, 710, 79), "SETTINGS", 61, Cream, FontStyle.Bold);
            Label(new Rect(444, 263, 500, 27), "MOUSE SENSITIVITY", 15, Cream, FontStyle.Bold);
            Label(new Rect(1020, 263, 135, 27), session.Sensitivity.ToString("0.00"), 21, Mint,
                FontStyle.Bold, TextAnchor.MiddleRight);
            GUI.backgroundColor = Mint;
            session.Sensitivity = GUI.HorizontalSlider(new Rect(445, 308, 710, 30), session.Sensitivity,
                0.25f, 5f, sliderStyle, thumbStyle);
            Label(new Rect(444, 369, 500, 27), "MASTER VOLUME", 15, Cream, FontStyle.Bold);
            Label(new Rect(1020, 369, 135, 27), Mathf.RoundToInt(session.MasterVolume * 100) + "%", 21, Mint,
                FontStyle.Bold, TextAnchor.MiddleRight);
            session.MasterVolume = GUI.HorizontalSlider(new Rect(445, 414, 710, 30), session.MasterVolume,
                0f, 1f, sliderStyle, thumbStyle);
            GUI.backgroundColor = Color.white;
            Fill(new Rect(444, 477, 711, 1), Alpha(Muted, 0.25f));
            Label(new Rect(444, 499, 710, 26), "CONTROLS", 13, Mint, FontStyle.Bold);
            Label(new Rect(444, 538, 350, 91),
                "WASD     Move\nShift        Sprint  /  Alt  Walk\nSpace      Jump  /  Ctrl  Crouch\nC              Slide  /  Q  Dodge", 16, Muted);
            Label(new Rect(820, 538, 340, 91),
                "LMB / RMB     Fire / Aim\nR                      Reload\n1 / 2 / 3           Switch weapon\nMouse wheel   Camera distance", 16, Muted);
            if (Button(new Rect(444, 683, 711, 64), "SAVE & RETURN", true))
            {
                session.SaveSettings();
                InvokeAndExit(session.CloseSettings);
            }
            Label(new Rect(444, 763, 711, 23), "Settings are saved locally between sessions.", 12,
                Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        void Stat(Rect rect, string title, string value, Color color)
        {
            Fill(rect, new Color(1, 1, 1, 0.035f));
            Label(new Rect(rect.x + 12, rect.y + 12, rect.width - 24, 23), title, 11, Muted,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            Label(new Rect(rect.x + 10, rect.y + 42, rect.width - 20, 47), value, 31, color,
                FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        void DrawTelemetry(float x, float y, string text, Color color)
        {
            float blink = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 2.5f);
            Fill(new Rect(x, y + 8, 6, 6), Alpha(color, blink));
            Label(new Rect(x + 18, y, 660, 23), text, 12, color, FontStyle.Bold);
        }

        bool Button(Rect rect, string title, bool primary)
        {
            bool hovered = rect.Contains(Event.current.mousePosition) && GUI.enabled;
            Color fill = primary ? Mint : new Color(1, 1, 1, hovered ? 0.1f : 0.035f);
            if (primary && hovered) fill = Color.Lerp(Mint, Cream, 0.22f);
            if (!GUI.enabled) fill = Alpha(fill, 0.25f);
            Fill(rect, fill);
            if (!primary)
            {
                Fill(new Rect(rect.x, rect.y, 2, rect.height), hovered ? Mint : Alpha(Muted, 0.4f));
                Fill(new Rect(rect.x, rect.yMax - 1, rect.width, 1), Alpha(Muted, 0.16f));
            }
            Color textColor = primary ? Ink : (hovered ? Mint : Cream);
            buttonStyle.normal.textColor = buttonStyle.hover.textColor = textColor;
            buttonStyle.active.textColor = primary ? Ink : Mint;
            buttonStyle.focused.textColor = textColor;
            return GUI.Button(rect, title, buttonStyle);
        }

        void Label(Rect rect, string text, int size, Color color, FontStyle weight = FontStyle.Normal,
            TextAnchor alignment = TextAnchor.UpperLeft)
        {
            if (!labels.TryGetValue(size, out GUIStyle style))
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size, padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0), wordWrap = false,
                    clipping = TextClipping.Clip, richText = false
                };
                labels[size] = style;
            }
            style.normal.textColor = color;
            style.fontStyle = weight;
            style.alignment = alignment;
            GUI.Label(rect, text ?? "", style);
        }

        static void Card(Rect rect, Color accent)
        {
            Fill(rect, Panel);
            Fill(new Rect(rect.x, rect.y, 3, rect.height), accent);
            Fill(new Rect(rect.x + 3, rect.y, rect.width - 3, 1), Alpha(Cream, 0.12f));
            Fill(new Rect(rect.xMax - 18, rect.y + 8, 10, 2), Alpha(accent, 0.6f));
        }

        static void Bar(Rect rect, float value, Color color)
        {
            Fill(rect, new Color(1, 1, 1, 0.095f));
            Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), color);
            if (rect.height >= 8)
                for (int i = 1; i < 10; i++)
                    Fill(new Rect(rect.x + rect.width * i / 10f, rect.y, 2, rect.height), Alpha(Ink, 0.4f));
        }

        static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Line(Vector2 start, Vector2 end, float thickness, Color color)
        {
            Matrix4x4 old = GUI.matrix;
            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, start);
            Fill(new Rect(start.x, start.y - thickness * 0.5f, Vector2.Distance(start, end), thickness), color);
            GUI.matrix = old;
        }

        static Color Alpha(Color color, float alpha) { color.a = alpha; return color; }
        static float Ease(float t) { return 1f - (1f - t) * (1f - t) * (1f - t); }

        static void InvokeAndExit(Action action)
        {
            action();
            GUIUtility.ExitGUI();
        }
    }
}
