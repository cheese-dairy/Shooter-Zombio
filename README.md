# DEAD SIGNAL

A self-contained third-person zombie survival game for **Unity 6.0**, built entirely from primitive geometry, generated materials, synthesized sound, and C#.

The project implements a seeded abandoned district, camera-relative movement, three weapons, six enemy archetypes, endless waves, supplies and temporary boosts, scoring, and a custom animated HUD with main menu, settings, pause, and game-over screens. No models, textures, animation packs, audio downloads, NavMesh bake, or Inspector reference wiring are required.

**Validation status:** the production navigation/wave logic passes 19 executable checks. The runtime scripts also compile against Unity 2022.3 API reference assemblies in a supplemental compatibility check. Unity 6 is not installed in the authoring cloud environment: editor import, the supplied Unity smoke suite, rendering, input feel, balance, and player builds have **not** been run here. This is a source project, not a tested standalone release. See [validation details](docs/VALIDATION.md).

## Open and play

1. Install **Unity Hub** and **Unity Editor 6000.0.23f1** (Unity 6.0). Add the desktop build-support module for your target platform. Use your normal Unity license activation.
2. In Hub, choose **Add → Add project from disk** and select this directory, the one containing `Assets`, `Packages`, and `ProjectSettings`. Do not nest it inside another Unity project. A later Unity 6.0 patch may work, but the pinned version is the intended baseline.
3. Open the project and let Unity import it. The editor setup automatically configures the **Built-in Render Pipeline**, legacy input, retained runtime shaders, and the build scene.
4. Open `Assets/Scenes/Main.unity` (or **Dead Signal → Open Main Scene**).
5. Press **Play**, choose a district seed, then select **Deploy into the district**. Click the Game view if necessary to give it keyboard/mouse focus.

The small saved scene is intentional. `GameSession` builds the complete playable hierarchy at runtime. The same bootstrap also runs when opening a fresh empty scene in this dedicated project. All references are assigned by code. Every generated model is replaceable without removing the actor's gameplay component.

If configuration needs refreshing, use **Dead Signal → Configure Project** before Play. Keep **Active Input Handling = Input Manager (Old)** and the render-pipeline asset unset; accept Unity's restart if you change the input backend. Do not install the Input System or URP to run this project.

## Controls

| Action | Control |
| --- | --- |
| Camera-relative run | WASD or arrow keys |
| Walk | Hold Left Alt |
| Sprint | Hold Left Shift; consumes stamina |
| Jump | Space |
| Crouch | Hold Left Ctrl |
| Momentum slide | C while grounded and sprinting |
| Dodge | Q while grounded; costs stamina; brief opening invulnerability |
| Orbit camera / zoom | Mouse / wheel |
| Aim / fire | Right / left mouse button |
| Reload | R; empty magazines also initiate reload when reserve exists |
| Sidearm / carbine / shotgun | 1 / 2 / 3; find weapon caches to unlock slots |
| Pick up supplies | Walk within 1.5 metres; full-health players leave medkits |
| Pause / resume / close settings | Escape |
| Survival hint | Tab |

Switching weapons cancels an unfinished reload without spending reserve ammo. A second ray from the weapon and a chest-to-muzzle obstruction test keep nearby cover from being bypassed. Movement, aiming, recoil, camera collision, procedural limb movement, sound, muzzle flashes, and hit confirmations provide feedback without animation assets.

## Survival loop

You begin with a sidearm. A carbine cache sits six metres north of the spawn; another weapon cache unlocks the shotgun elsewhere. Search colored supply markers for ammo, health, weapons, damage, speed, rapid fire, and score rewards. Extra ammunition appears every 35 seconds, and kills can drop supplies. Boosts last 18 seconds and stack duration. Three or more quick kills trigger streak feedback and increasing score bonuses.

The opening countdown is five seconds. A wave completes only when both queued and living enemies reach zero. Intermissions last 12 seconds. Standard zombies appear in waves 1–2; runners join at 3, brutes at 5, and spitters/exploders at 7. Every tenth wave includes the Warden, whose volleys and marked radial attack demand movement and cover. Exploders telegraph before detonating; shooting them first earns a kill, while their self-detonation removes them without awarding a player kill.

Counts, simultaneous enemies, health growth, and damage growth have caps. This keeps endless progression bounded; final difficulty and resource balance still need real playtesting. Death offers retry or main menu; best score, sensitivity, and volume persist locally. A new run resets player state, weapons, enemies, projectiles, supplies, and score.

## Tune and extend

Select **`Assets/Resources/GameTuning.asset`** outside Play mode. Its Inspector persists player health/stamina/speeds/jump height, camera distance/shoulder offset, wave rules, concurrency, intermission, spawn pacing, and pickup chance. Leave `Weapons` empty for the shipped definitions, or supply exactly three fully populated entries in sidearm/carbine/shotgun order. Invalid weapon arrays fall back to the shipped definitions. No dragged scene references are needed.

The detailed [script reference and setup guide](docs/SCRIPT_REFERENCE.md) lists **every C# filename**, exact location, GameObject, required components, Inspector defaults, and connections. It also explains the hierarchy, enemy factories, navigation, replacing primitive visuals, and adding enemy types.

The district uses a connected 43×43 grid with two-metre cells. Buildings and cover occupy the same cells used by A* navigation. Sealed buildings avoid inaccessible interiors; roads, courtyards, alleys, boundary gates, rooftops, lamps, markings, and fog establish the environment. Seeds reproduce architecture and initial supplies. Enemies use CharacterControllers and grid waypoints, not NavMeshAgents. See the script reference before adding collidable objects.

## Checks and builds

With .NET 8 installed, run the actual engine-independent logic:

```sh
dotnet run --project Tools/CoreChecks/CoreChecks.csproj --configuration Release
```

For the supplied Linux x64 cloud environment, `bash Tools/setup-core.sh` installs a pinned, SHA-512-verified SDK under `/workspace/tooling` if necessary and runs those checks. It leaves Unity installation/licensing to the normal Unity workflow. Each cloud task is already isolated; use the existing checkout and do not create a Git worktree unless requested.

With a licensed Unity editor, run the engine integration suite (replace the executable path):

```sh
/path/to/Unity -batchmode -nographics \
  -projectPath "$PWD" \
  -executeMethod Zombio.Editor.SmokeChecks.Run \
  --zombio-smoke -logFile /tmp/dead-signal-smoke.log
```

**Do not add `-quit`** to the smoke command: the suite exits with its own pass/fail code. It temporarily uses accelerated simulation and checks generation/reachability, real weapon raycasts and cover, reloads, damage/healing, pause, ten completed waves, the boss, all enemy archetypes, game over, and restart. It uses the shipped tuning values. It does not replace visual and input playtesting.

For a Linux player, install Linux Build Support and choose **Dead Signal → Build Linux Player**. The output is `Builds/Linux/DeadSignal.x86_64`. For Windows or macOS, use **File → Build Profiles**, select the platform, and keep `Assets/Scenes/Main.unity` enabled. The source contains no platform-specific gameplay code. The editor setup retains the shaders used by generated materials to prevent pink/missing materials in builds.
