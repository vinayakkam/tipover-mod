# In-game test kit

Needs [kOS](https://github.com/KSP-KOS/KOS). Stock parts only.

Install:
- `Script/*` -> `<KSP>/Ships/Script/` (the kOS archive, `0:/`)
- `RF-Test.craft` -> `<KSP>/saves/<save>/Ships/VAB/`

Launch RF-Test from the pad. The boot script `0:/rftest.ks` runs one test per launch:
1. Pad (must survive), hop, soft landing (must survive), tip over with the reaction wheel (must explode).
2. Revert to Launch: descend at 5.5 m/s onto the pad (must explode). Needs `impactSpeed = 4` in `settings.cfg`, because stock parts break at about 7 m/s, before a 20 m/s impact could be detected.

Results: `Ships/Script/rftest_log.txt` and `[RocketFailure]` lines in `KSP.log`. Delete `Ships/Script/rftest_mode.txt` to start over.
