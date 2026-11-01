# RocketFailure (KSP 1 mod)

- Single source file: `src/RocketFailure.cs` (`KSPAddon` Flight-scene MonoBehaviour). Keep it single-file, no dependencies.
- Settings: `GameData/RocketFailure/settings.cfg` (node `ROCKET_FAILURE`), re-read on each flight scene load. New setting = static field + `TryGetValue` + cfg entry + README line.
- Build: `dotnet build -c Release -p:KSPDIR="<KSP install>"`. Output auto-copied to `GameData/RocketFailure/Plugins/`. DLLs are gitignored.
- Design rules: poll `FlightGlobals.VesselsLoaded` at 4 Hz (no per-frame work, no GameEvents subscriptions); skip packed, PRELAUNCH, EVA, flags, asteroids; destroy only via `Part.explode()` outside the vessel loop; remove vessel state before exploding to prevent repeats.
- Target C# 7.3 / net472 (KSP 1.12 Unity 2019 runtime).
