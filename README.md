# RocketFailure

KSP 1 mod: rockets that tip over after landing, tip or sink after splashdown, or touch down too hard explode.

## How it works
- **Tipping:** a landed vessel tilted more than `tiltAngle` from the local terrain normal (raycast) for `failureDelay` seconds explodes. A vessel is only checked after it has been seen standing upright (tilt < half the limit) and is at least `minVesselHeight` tall. Planes, rovers, small capsules and vessels loaded already lying down are therefore ignored.
- **Water:** same check against vertical with `splashTiltAngle`, but a vessel falling over in the water explodes the moment its top part hits the water (no `failureDelay`). Optionally, a splashed vessel whose centre of mass is more than `sinkDepth` m below sea level explodes.
- **Impact:** touching down with a descent rate above `impactSpeed` m/s (last airborne sample) explodes the vessel at once. Horizontal speed is ignored, so normal runway landings are safe.
- **Chance:** `failureChance` < 1 lets a vessel survive; it is not re-rolled until the condition clears.
- Destruction uses `Part.explode()` (KSP's own explosion FX). A few light parts (`debrisParts`) survive as wreckage; after `debrisSeconds` they lose buoyancy and colliders, sink into the water or ground and are removed. Pad/runway (PRELAUNCH), EVA kerbals, flags, asteroids and packed (on-rails) vessels are never touched. Spent stages (debris vessels) are not checked unless `includeDebris` is on.
- Cost: one pass over loaded vessels 4 times a second, no allocations in steady state, no event subscriptions.

## Build
Needs the .NET SDK (6+) and a KSP 1.12 install.
```
dotnet build -c Release -p:KSPDIR="/path/to/Kerbal Space Program"
```
`KSPDIR` defaults to the Linux Steam path. The DLL is copied to `GameData/RocketFailure/Plugins/RocketFailure.dll`.

## Install
Copy `GameData/RocketFailure` into your KSP `GameData` folder:
```
GameData/RocketFailure/settings.cfg
GameData/RocketFailure/Plugins/RocketFailure.dll
```

## Configuration
Edit `GameData/RocketFailure/settings.cfg`. Changes apply on the next flight scene load (revert or switch vessel).

| Key | Default | Meaning |
|---|---|---|
| enabled | true | master switch |
| tippingEnabled / tiltAngle | true / 60 | landed tipping |
| splashTippingEnabled / splashTiltAngle | true / 75 | tipping in water |
| minVesselHeight | 2.5 | metres between lowest and highest part centre |
| sinkingEnabled / sinkDepth | true / 10 | submersion |
| impactEnabled / impactSpeed | true / 20 | hard touchdown |
| failureDelay | 3 | grace period, seconds |
| failureChance | 1 | 0-1 |
| includeDebris | false | check spent stages |
| debrisParts | 3 | light parts left as wreckage after an explosion (0 = none) |
| debrisSeconds | 20 | seconds before the wreckage sinks and is removed |

## Testing
1. Land a tall rocket upright, then tip it with reaction wheels/RCS past 60 degrees. Expect "Vessel tipping over!" then an explosion after 3 s.
2. Land at a slight angle (< 60 degrees): nothing happens.
3. Splash a capsule under parachutes: nothing happens (too short). Splash a tall stage upright and tip it: explodes.
4. Drop a sturdy vessel at > 20 m/s vertical: explodes on touchdown.
5. Launch normally from the pad: nothing happens.
Log lines are prefixed `[RocketFailure]` in `KSP.log`.

Automated test with kOS (stock pad-hop rocket): see [test/README.md](test/README.md).

## Known limitations
- Untested in game so far; see the commit history for build status.
- A rocket that is already tilted more than half the limit when it first touches down is never armed, so it will not explode if it then falls over.
- Tilt uses the control point's "up" axis. "Control from here" on a sideways part changes what counts as upright.
- Impact speed is sampled at 4 Hz, so it can be off by about 2 m/s on Kerbin.
- Vessel height uses part centres, not part bounds.
