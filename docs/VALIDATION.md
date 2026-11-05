# Validation record

## Executed in the cloud environment

- Installed official .NET SDK **8.0.427**, verifying the archive against Microsoft's published SHA-512 digest before extraction.
- Executed `Tools/setup-core.sh` to check the reusable setup path.
- Ran **19 core checks: 19 passed, 0 failed**. These compile and execute the production `GridNavigation` and `WaveRules` code, including 180 randomized routes compared to an independent Dijkstra oracle, blocked/unreachable/corner cases, deterministic composition, milestone gates, boss intervals, and count/stat caps.
- Compiled all runtime C# sources (including the opt-in smoke runner) against **Unity 2022.3** reference assemblies: **0 errors, 0 warnings**. This is a supplemental API compatibility check; it is not a Unity 6 editor build, and excludes `Assets/Editor` and `UNITY_EDITOR` branches.
- Checked asset GUID uniqueness, scene/build references, package JSON, script coverage in the reference guide, and archive contents.

The optional API compiler project is `Tools/ApiCompile/ApiCompile.csproj`. It uses the pinned NuGet package `RocketModFix.UnityEngine.Redist` **2022.3.62.3** as reference metadata only. This third-party redistribution is **not** a dependency of the Unity project and no game code is executed against it. The lock file pins the package content hash. Reproduce it with:

```sh
dotnet restore Tools/ApiCompile/ApiCompile.csproj --locked-mode
dotnet build Tools/ApiCompile/ApiCompile.csproj --no-restore
```

## Supplied but not executed here

Unity is absent from this environment. The following remain unverified:

- Unity 6 editor import and editor-only tooling compilation.
- `Zombio.Editor.SmokeChecks.Run` and the runtime integration assertions.
- Real input, controller physics, enemy navigation around geometry, camera behavior, lighting/shaders, menus, audio, performance, and difficulty through a full run.
- Standalone desktop builds and installation in a fresh cloud task.

The runtime suite is opt-in (`--zombio-smoke`) and exits with code 0 only after its assertions complete; an exception, logged Unity error, failed assertion, or timeout returns failure. Read the actual log for `DEAD SIGNAL SMOKE PASS` and the assertion count. No zero-test or syntax-only result is treated as a gameplay pass.

## Manual Unity acceptance pass

1. Open Main, enter Play, deploy with seed 1408. Confirm the menu/HUD renders without missing shaders/fonts and the player is visible.
2. Test WASD and camera-relative turns; walk, sprint until stamina empties, recharge, jump, crouch, slide, and dodge. Test against walls and cover; inspect camera obstruction when backing into a corner.
3. Aim/fire at every archetype. Check crosshair/muzzle agreement, shot obstruction at close cover, ammo, reload completion/cancellation, all three weapons, sound, hit feedback, and death effects.
4. Collect all seven pickup kinds. Leave a medkit at full health, damage the player, then collect it. Check boost expiration and duration extension.
5. Clear waves through 10; check the remaining count includes queued enemies, wave completion waits for all survivors, new archetypes arrive at the intended waves, and the boss health bar and attack warnings work.
6. Start different seeds and revisit the same seed. Check reachable gates/supplies, varied architecture, no wall spawn intersections, no stuck enemies, and no surviving actors/effects from the previous run.
7. Pause, settings, resume, focus loss, game over, retry, and return to menu. Check time/input state, reset inventory and score, and saved volume/sensitivity/best score.
8. Run a desktop build at 1280×720, 1600×900, and an ultrawide resolution. Check readable HUD and menus, mouse capture/release, scene inclusion, shaders, sound, and performance.

Report actual Unity results before describing the project as a fully validated game release.
