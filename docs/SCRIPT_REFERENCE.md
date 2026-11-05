# Complete script and setup reference

All implementation is delivered in `Assets/Scripts` and `Assets/Editor`. Open the repository root as a Unity 6 project, open `Assets/Scenes/Main.unity`, and enter Play mode. `GameSession` creates and connects every gameplay object automatically. Select **Start run** in the game menu to start the countdown. No model downloads, prefab downloads, Inspector reference dragging, Canvas setup, Animator setup, Rigidbody setup, or NavMesh baking is required.

The shipped scene is deliberately minimal. The generated hierarchy appears after entering Play mode. Adding a second player, camera rig, HUD, or wave director manually would duplicate the existing setup. Public component fields can be inspected and adjusted on their generated objects during Play mode; Unity discards such changes when leaving Play mode. To retain a tuning change, update its default initializer in the corresponding script. To expose a fully hand-authored scene later, replace `GameSession.Awake` construction with serialized references as a separate customization.

## Generated hierarchy

```text
GameSession                                  GameSession
├── Procedural District                      WorldGenerator
│   └── Generated District [seed ...]        primitive roads, buildings, lights, fog settings
├── Survivor                                 CharacterController, PlayerController, WeaponController
│   ├── Visuals                              replaceable primitive character parts
│   └── WeaponRig                            weapon body, muzzle, muzzle flash/light
├── Shoulder Camera                          Camera, AudioListener, ThirdPersonCamera
├── Synthesized Audio                        ProceduralAudio
│   └── Voice 0 ... Voice 15                  AudioSource
├── Wave Director                            WaveDirector
│   └── Enemies and hostile effects
│       ├── [kind] / wave [number]            EnemyActor, CharacterController
│       │   ├── Replaceable zombie model
│       │   └── Attack danger zone
│       ├── Acid projectile                  AcidProjectile, TrailRenderer
│       └── Bio impact                       EnemyBurst
├── Pickups                                  PickupDirector
│   └── [kind] Supply                        PickupActor
│       ├── Supply icon
│       └── Ground marker
└── Interface                                SurvivalHud
```

Short-lived `BulletTrail` and `ImpactSpark` objects are created by `ShotEffect`. Generated render-only primitives have their default colliders disabled and removed. Physical world geometry uses box colliders; actors use CharacterControllers. Ground-plane positions are feet positions, with Y = 0 at street level.

## Lifecycle, audio, and interface

| Exact file | Object and required components | Purpose, configuration, and connections |
| --- | --- | --- |
| `Assets/Scripts/Game/GameSession.cs` | `GameSession`; no extra required component | Static startup creates the session when a scene loads. `Awake` creates the hierarchy and calls each subsystem's `Configure` method. Inspector defaults: `Seed = 1408`, `Sensitivity = 2`, `MasterVolume = 0.65`. Settings and best score persist through PlayerPrefs. `StartRun(seed)` clears prior enemies/supplies, generates the map, resets the player/weapons, seeds supplies, and begins waves. Owns menu, pause, settings, death, score, streak bonuses, and audio/HUD forwarding. No dragged references. |
| `Assets/Scripts/Audio/ProceduralAudio.cs` | `Synthesized Audio`; creates 16 child AudioSources | `Initialize` synthesizes shot, hit, kill, pickup, reload, hurt, wave, explosion, and click sounds. `Play(SoundCue, position)` selects a voice and uses the global session volume. No Inspector fields or references. The camera's AudioListener is created by the session. |
| `Assets/Scripts/UI/SurvivalHud.cs` | `Interface`; no Canvas or EventSystem required | Draws the custom responsive IMGUI interface, main menu, settings, pause menu, game-over screen, health/stamina/ammo HUD, crosshair, boss bar, countdown, kill feedback, warnings, and notifications. `Configure(session)` supplies all references. `Notify`, `Announce`, `HitConfirmed`, and `DamageFeedback` drive animated feedback. No Inspector fields. Menu sensitivity and volume controls update `GameSession`; save uses PlayerPrefs. |

The interface uses built-in font support and code-generated textures. UI animation uses unscaled time so menus and feedback can animate while gameplay is paused. Gameplay systems check `session.IsPlaying`; menu, pause, settings, and game-over states set gameplay time scale to zero. Escape pauses/resumes and closes settings; losing application focus pauses a run.

## Player, camera, and weapons

| Exact file | Object and required components | Purpose and automatic connections |
| --- | --- | --- |
| `Assets/Scripts/Player/PlayerController.cs` | `Survivor`; CharacterController required and supplied by session | Camera-relative movement, walking/running/sprinting, stamina, buffered jumping, crouch clearance, momentum slide, dodge, health, damage, death, speed boosts, and primitive limb animation. `Configure(session, cameraRig)` sets the body and creates `Visuals`. `TakeDamage` invokes session HUD/audio/death handling; pickups call `Heal` and `AddSpeedBoost`. This file also contains the non-component `PlayerGeometry` helper that makes primitive parts/materials. |
| `Assets/Scripts/Player/ThirdPersonCamera.cs` | `Shoulder Camera`; Camera required, AudioListener supplied by session | `Configure(player)` initializes follow position and heading. Mouse orbit, scroll zoom, shoulder offset, aim zoom, FOV response, recoil, and collision avoidance run in `LateUpdate`. Sphere casts ignore the player's own collider. Session settings override the default sensitivity. `Shake` and `Kick` are called by combat and damage systems. |
| `Assets/Scripts/Player/WeaponController.cs` | `Survivor`, beside PlayerController | `Configure(session, player, cameraRig)` creates the weapon rig and primitive visuals. Contains serializable `WeaponDefinition` data, three weapon slots, magazine/reserve state, switching, reload timing, buffs, camera ray targeting, muzzle obstruction checking, recoil, muzzle flash, and hit feedback. Calls `EnemyActor.TakeDamage`; pickups call `UnlockWeapon`, `AddAmmo`, `AddDamageBoost`, and `AddRapidFireBoost`. No external weapon prefab or dragged reference. |
| `Assets/Scripts/Player/ShotEffect.cs` | Generated `BulletTrail` / `ImpactSpark`; creates LineRenderer or primitive renderer | `Tracer(from, to, color)` and `Impact(position, color)` create short-lived effects. The effect destroys itself and its owned material. No Inspector setup or references. |

`PlayerController` Inspector defaults and meaning:

| Field | Default | Meaning |
| --- | --- | --- |
| `MaxHealth`, `MaxStamina` | 100, 100 | Starting capacities restored for each new run. Keep positive. |
| `WalkSpeed`, `RunSpeed`, `SprintSpeed` | 2.4, 4.9, 8 | Ground speed in metres per second. Default movement runs; Left Alt walks. |
| `JumpHeight` | 1.4 | Jump apex above takeoff, in metres. |
| `Gravity` | 25 | Downward acceleration magnitude. |
| `Acceleration` | 14 | Movement response rate; higher values approach target velocity faster. |
| `TurnSpeed` | 15 | Rotation response rate. |
| `DodgeCost`, `SlideCost` | 25, 16 | Stamina spent when beginning each action. |

The controller sets standing height to 1.9 m, crouched height to 1.15 m, radius to 0.34 m, step offset to 0.3 m, slope limit to 48°, and skin width to 0.035 m. The collider center tracks half its current height to keep the feet grounded. Input uses the legacy Unity Input Manager; the project supplies the needed horizontal, vertical, and mouse axes.

`ThirdPersonCamera` Inspector defaults: `FollowSharpness = 18` (follow smoothing), `RotationSensitivity = 2` (fallback sensitivity), `Distance = 5` (normal follow distance), `ShoulderOffset = 0.6` (horizontal offset), `MinimumPitch = -25`, `MaximumPitch = 65` (vertical orbit limits). The settings menu controls the live sensitivity. The zoom wheel clamps normal follow distance between 3.5 and 7.5 m; aiming moves it to 2.65 m. No target needs dragging into the Inspector.

`WeaponController.Definitions` is an expandable array. Each entry exposes `Name`, `Magazine`, `StartingReserve`, `Damage` per pellet, `ShotInterval` in seconds, `ReloadSeconds`, `Pellets`, `Spread` in degrees, `Recoil`, and `Accent` color. Keep magazine/pellet counts positive and timing values greater than zero. The shipped entries are:

| Slot | Weapon | Magazine / reserve | Damage / pellets | Shot interval | Reload |
| --- | --- | --- | --- | --- | --- |
| 1 | M-12 SIDEARM | 12 / 72 | 32 / 1 | 0.24 s | 1.35 s |
| 2 | R-30 CARBINE | 30 / 120 | 22 / 1 | 0.105 s | 1.85 s |
| 3 | BREACH-6 | 6 / 36 | 15 / 8 | 0.8 s | 2.2 s |

Every new run starts with the sidearm unlocked; weapon pickups unlock the other slots. `WeaponDefinition` is data, not an attachable component. For additional weapons, extend the definition array and update the input/pickup selection mapping if you want more than the three shipped numeric hotkeys. Visual geometry can be replaced under `WeaponRig` while retaining the aiming and muzzle transforms.

## Procedural world and navigation

| Exact file | Object and required components | Purpose, fields, and connections |
| --- | --- | --- |
| `Assets/Scripts/World/WorldGenerator.cs` | `Procedural District`; no additional component | `Generate(seed)` creates an 86 × 86 m district with 16 varied blocks, reserved connected roads, alleys, cover, boundary walls, lights, fog, eight spawn gates, and supply positions. Inspector field `Seed` defaults to 73421, but the session passes its menu-selected seed and overwrites it. `PlayerSpawn`, `SpawnPoints`, `PickupPoints`, and `GeometryRoot` are read-only runtime results. Enemy actors call `GetWaypoint(from, goal)`; supplies use `IsWalkable`. Grid dimensions (`GridSize = 43`, `CellSize = 2`) are code constants. No Inspector references. |
| `Assets/Scripts/Core/GridNavigation.cs` | None; plain C# classes | `GridCell` represents X/Z coordinates. `GridNavigation(bool[,])` clones a walkability grid. `FindPath(start, goal)` returns a deterministic eight-way A* route including both endpoints, or an empty route when unreachable/invalid. Diagonal moves cannot cut blocked corners. `IsWalkable`, `Width`, and `Height` support callers and checks. WorldGenerator owns and queries this class. No Unity components or Inspector setup. |

Road connectivity is reserved before building placement. Obstacle footprints are represented in the same grid used for navigation; decorative parts do not unexpectedly block paths. Actor motion remains collision-aware through CharacterController.Move. Enemies periodically refresh their next waypoint and use local separation. This is runtime grid navigation: do not add NavMeshAgent or perform a bake. New collidable obstacles must also update the walkability grid; visual decorations can remain collider-free.

Changing the main-menu seed and starting a run regenerates the district and resets all actors. A repeated seed reproduces architecture and initial supply placement. Combat effects may use independent random variations.

## Enemy actors and wave progression

| Exact file | Object and required components | Purpose, fields, and connections |
| --- | --- | --- |
| `Assets/Scripts/Core/WaveRules.cs` | None; serializable plain C# class | Declares `EnemyKind` and all count/scaling/type-weight rules. WaveDirector owns a `Rules` instance exposed in its Inspector. Contains no Unity dependency. `ChooseEnemy` receives a seeded System.Random; boss selection is handled separately by the director. |
| `Assets/Scripts/Enemies/WaveDirector.cs` | `Wave Director`; no extra component | `Configure(session)` supplies the world and player. `Begin` starts an opening countdown. Spawns actor roots and calls `EnemyActor.Configure`; tracks pending and living enemies so a wave cannot finish early. `OnEnemyDied` unregisters exactly one actor; `Clear` removes prior runtime actors/projectiles. Inspector fields are documented below. No prefab array or dragged references. |
| `Assets/Scripts/Enemies/EnemyActor.cs` | Each generated enemy root; creates a CharacterController | `Configure(session, director, kind, healthScale, damageScale)` sets stats, builds a distinct primitive model, and creates the collider. Implements chase/path following, melee windups, ranged spitting, explosive self-destruction, boss slams/volleys, hit reactions, damage, and death. `TakeDamage(damage, hitPoint)` is the combat entry point. Death informs both the director and session for counts, rewards, and drops. Runtime properties `Kind`, `Health`, `MaxHealth`, `IsDead` have no manual Inspector setup. |
| `Assets/Scripts/Enemies/EnemyVisuals.cs` | None; static helper | Creates colored primitive model parts and caches shared materials; builds impact effects via EnemyBurst. Used only by enemy/projectile code. No Inspector fields or references. |
| `Assets/Scripts/Enemies/AcidProjectile.cs` | Generated acid projectile; creates a TrailRenderer | `Configure(session, owner, direction, speed, damage, color)` supplies all state. A swept sphere test prevents fast projectiles passing through colliders. The owner is ignored, cover blocks the projectile, and the player receives damage only during an active run. Self-destructs on hit or timeout. No Rigidbody or manually attached collider required. |
| `Assets/Scripts/Enemies/EnemyBurst.cs` | Generated `Bio impact`; creates seven primitive shards | `Configure(color, radius)` creates and animates a brief burst that expires automatically. Owned under the wave runtime root so run cleanup removes it. No Inspector fields or references. |

Enemy base stats (health and damage are multiplied by the wave rules):

| Kind | Health | Speed | Damage | Behavior / identifying color |
| --- | --- | --- | --- | --- |
| Standard | 70 | 2.3 | 12 | Approaches and melees; teal. |
| Runner | 45 | 5.2 | 9 | Fast chase and quicker melee; orange. |
| Brute | 260 | 1.65 | 27 | Larger armored body, slower heavy melee; violet. |
| Spitter | 85 | 2.6 | 13 | Maintains range and launches acid; lime. |
| Exploder | 75 | 3.5 | 36 | Approaches, telegraphs, then detonates; coral. |
| Boss | 1250 | 2.1 | 25 | Large armored Warden with melee, volleys, and radial slam; crimson. |

To add another enemy class, extend `EnemyKind`, add its stats/behavior/visual variant in EnemyActor, and add its eligibility/weight in WaveRules. The session, damage entry point, and wave tracking remain shared. To replace a model, preserve the actor root, collider, and damage component, and replace the `Replaceable zombie model` children. Runtime primitive factories serve the role of prefabs; no prefab authoring is needed to play the delivered game.

WaveDirector Inspector fields: `MaximumAlive = 22` limits concurrent enemies; `IntermissionSeconds = 12` sets rest between completed waves; `SpawnInterval = 0.85` sets initial spawn pacing, which tightens gradually to a 0.25 s floor; `MinimumSpawnDistance = 16` prefers gates away from the player. Occupied gates are skipped, and spawning is postponed if the selected gate is blocked by another actor. `Remaining` includes living and still-pending enemies, while `AliveCount` counts only spawned survivors.

Expand **Rules** to edit:

| Field | Default | Meaning |
| --- | --- | --- |
| `InitialCount`, `ExtraPerWave`, `MaximumCount` | 6, 3, 72 | Base wave count, per-wave increase, and cap. A boss replaces one enemy in a boss wave. |
| `BossInterval` | 10 | Every tenth wave includes one boss; zero disables bosses. |
| `HealthGrowth`, `MaximumHealthMultiplier` | 0.075, 4 | Per-wave health growth and maximum multiplier. |
| `DamageGrowth`, `MaximumDamageMultiplier` | 0.035, 2.5 | Per-wave damage growth and maximum multiplier. |
| `RunnerWeight` | 0.22 | Eligible from wave 3. |
| `BruteWeight` | 0.15 | Eligible from wave 5. |
| `SpitterWeight`, `ExploderWeight` | 0.13, 0.12 | Eligible from wave 7. |

At least 38% of weighted selection remains standard zombies, even when special weights are raised. The finite population and stat caps keep endless progression bounded; weapon skill, mobility, and resource collection remain important. Bosses never come from ordinary weighted selection.

## Pickups and progression

| Exact file | Object and required components | Purpose, fields, and connections |
| --- | --- | --- |
| `Assets/Scripts/Pickups/PickupDirector.cs` | `Pickups`; no extra component | Declares `PickupKind`. `Configure(session)` wires it; `SeedSupplies` uses world supply points and the run seed to place starting medkits, ammo, and weapon unlocks. `TryDrop(position, boss)` is called after kills. Inspector fields: `DropChance = 0.35`, `MaximumPickups = 40`. A timed ammo supply every 35 s makes recovery possible after missed shots. No prefab references. |
| `Assets/Scripts/Pickups/PickupActor.cs` | Generated `[kind] Supply`; creates primitive icon/ground marker | `Configure(session, kind, weaponIndex)` wires the pickup and builds its color-coded visuals. Rotates/bobs, collects within 1.5 m, applies its reward once, plays audio, and notifies the HUD. Proximity collection needs no trigger or Rigidbody. Full-health players leave medkits in place. No Inspector fields or references. |

Health restores 35 points, ammo adds reserve rounds, weapon items unlock a slot, damage/speed/rapid-fire effects last 18 seconds, and score caches grant 250. Distinct green/amber/blue/red/cyan/violet/gold colors identify the seven pickup kinds. Kill streaks within four seconds grant increasing bonuses through GameSession; the high score persists locally.

## Editor tooling and standalone checks

`Assets/Editor/ProjectSetup.cs` is an editor-only static class, not a component. It configures the built-in renderer, linear color, legacy input, window defaults, scene list, and runtime-found shader retention. Its **Dead Signal** menu exposes configuration, scene opening, and Linux build commands. The Linux build requires the Unity Linux Build Support module; other platforms can use Unity Build Profiles with `Assets/Scenes/Main.unity` enabled. Editor scripts live under `Assets/Editor` and are excluded from player assemblies.

`Tools/CoreChecks/Program.cs` is a standalone .NET console executable, not an Assets script and not a Unity component. `CoreChecks.csproj` links the actual production `GridNavigation.cs` and `WaveRules.cs`. Run:

```sh
dotnet run --project Tools/CoreChecks/CoreChecks.csproj --configuration Release
```

The harness exits nonzero on failure. Its 19 checks cover path validity, unreachable/invalid cells, corner cutting, immutable inputs, deterministic routes, 180 random routes compared with an independent Dijkstra solver, wave bounds, scaling, milestone eligibility, seeded compositions, configurable weights, and boss intervals. These checks do not validate Unity input, physics, rendering, audio, editor import, or player builds; those need the Unity editor workflow described in the README.

## Persistent tuning and engine smoke checks

| Exact file | Placement / requirements | Purpose and Inspector setup |
| --- | --- | --- |
| `Assets/Scripts/Game/GameTuning.cs` | ScriptableObject at `Assets/Resources/GameTuning.asset`; not attached to a GameObject | Loaded by `GameSession` through Resources. Persistently edits player health/stamina/movement/jump, camera distance/shoulder, all WaveRules fields, max concurrent enemies, intermission, spawn interval and pickup chance. Defaults match the tables above. The optional `Weapons` array must contain exactly three complete WeaponDefinitions in slot order; leave empty for defaults. The editor recreates this asset if missing. No references need dragging. |
| `Assets/Scripts/Diagnostics/RuntimeSmokeChecks.cs` | Automatically attached to GameSession only when `--zombio-smoke` is present | Coroutine checks actual scene generation, supply paths, camera/muzzle shooting and penetrating-cover protection, reload, damage/heal, unlocks, pause, ten waves, all archetypes, death, and restart. Uses shipped tuning defaults and logs assertion outcomes. Exits Unity on pass/failure/timeout; never attach during normal play. No Inspector fields. |
| `Assets/Editor/SmokeChecks.cs` | Editor-only static class; no GameObject | `Run` prepares the project, opens Main, and enters Play mode for the opt-in runtime runner. Requires `--zombio-smoke`; omit `-quit` so the runner owns exit status. |

Tune the `GameTuning` asset **outside Play mode** for persistent changes. Inspector edits to generated scene objects during Play are temporary. Less common controller/visual parameters can be changed in their documented source defaults. `WeaponController.TryFire()` and `BeginReload()` are public guarded input entry points; keyboard/mouse handling and integration checks use the same implementation. An override's magazine, pellet count, damage and timings must be positive; reserves must be nonnegative.

`Tools/ApiCompile/ApiCompile.csproj` is an optional compile-only reference check. It is outside Assets and does not become a Unity assembly or game dependency. `Tools/setup-core.sh` provisions the pinned .NET SDK for Linux cloud tasks and invokes the production core behavior tests. See `docs/VALIDATION.md` for the precise scope of each check.
