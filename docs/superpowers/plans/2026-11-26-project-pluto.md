# Project Pluto KSP Mod Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A standalone KSP 1.12.x mod adding the Project Pluto / SLAM missile as one part: integrated
boosters that auto-detach, an air-only nuclear ramjet with a reactor plugin, 16 tossed 1 Mt warheads
using a scaled Sundial effect, and a bombing-run autopilot that only takes over when commanded.

**Architecture:** Same shape as the sister mod at `the Project Sundial source folder`
(generated `.mu` model, part config, C# PartModules, `build-mono.sh`). All game-independent maths
(reactor, radiation, ballistics, toss solver, terrain lookahead, PID) lives in `Source/Core` with no
UnityEngine or KSP references, so it is unit-tested with a plain Mono console runner. KSP glue
modules in `Source/` are thin and verified by compiling plus an in-game checklist.

**Tech Stack:** C# (Mono `mcs`, .NET 4.7.2-compatible), KSP 1.12.5 `Assembly-CSharp.dll` and
`UnityEngine*.dll`, Python 3 + Pillow 11.3.0 + taniwha `io_object_mu` `mu.py` for the model.

**Spec:** `docs/superpowers/specs/2026-11-25-project-pluto-design.md`

## Global Constraints

- Project root: `the repository root`. KSP install:
  `<KSP folder>`. Managed folder:
  `<KSP folder>/KSP.app/Contents/Resources/Data/Managed`.
- No dependency on any other mod (including Sundial) or DLC.
- Namespace `ProjectPluto`; every type name starts with `Pluto` or `ModulePluto`. No type or GameData
  path shared with Sundial. GameData folder `GameData/ProjectPluto`.
- Part name `projectPlutoSLAM`, category Engine, tech node `nuclearPropulsion`, tags
  `pluto slam nuclear ramjet missile tory`.
- Real sizes: body 26.8 m long, 1.5 m diameter. Loaded mass without boosters 27.5 t.
- Text never presents invented numbers as historical; README lists what is invented.
- `Source/Core/*.cs` may use only `System` / `System.Collections.Generic`.
- Downloading `io_object_mu` needs the user's explicit OK before Task 6.

## Review Focus

1. **Floating origin / Krakensbane:** warheads and breach sources far from the active vessel must not
   drift or jump when KSP shifts its world origin; positions are kept body-relative in doubles
   (Task 9 test `Ballistics_BodyRelativeStateUnchangedByFrameShift`).
2. **Reload mid-flight:** a save taken after booster jettison, mid-reactor-warm-up, or with warheads
   released reloads with boosters gone, correct mass, correct warhead count and the reactor running
   (Task 7 in-game check list items 4–6; core state round-trip test in Task 2).
3. **Releasing while slow or climbing:** a warhead released at low speed must still fall and detonate
   (safety timer 120 s) and never detonate inside the missile on the first frame (Task 4 test
   `Ballistics_ArmingDelayPreventsImmediateDetonation`).
4. **Bombing run engaged at odd moments:** commanding the run with no target, over water, upside
   down, or above 20 km must dive safely or refuse with a message, not crash into terrain
   (Task 5 tests `Run_NoTargetHoldsHeading`, `Run_RefusesAboveAtmosphere`).
5. **Several clouds at once:** two simultaneous 1 Mt effects plus Sundial installed must stay
   playable (Task 8 in-game check).

---

## File Structure

```
ProjectPluto/
  build-mono.sh                 compile GameData/ProjectPluto/Plugins/ProjectPluto.dll
  run-tests.sh                  compile Source/Core + Tests into Tests.exe and run it
  README.md
  Source/Core/PlutoConstants.cs     all spec numbers (one place)
  Source/Core/PlutoReactorModel.cs  reactor state machine
  Source/Core/PlutoRadiation.cs     dose maths
  Source/Core/PlutoBallistics.cs    warhead flight, impact prediction, toss release
  Source/Core/PlutoRunLogic.cs      terrain lookahead, PID, bombing-run state machine
  Source/PlutoAssembly.cs           assembly attributes
  Source/ModulePlutoBoosters.cs
  Source/ModulePlutoReactor.cs
  Source/PlutoRadiationScenario.cs  per-kerbal doses + breach sources (ScenarioModule)
  Source/ModulePlutoWarheads.cs
  Source/PlutoWarheadProjectile.cs
  Source/ModulePlutoAutopilot.cs
  Source/Effect/Pluto*.cs           copied + renamed Sundial effect classes
  Source/BuildPluto.py              model + texture generator
  Tests/TestRunner.cs, Tests/*Tests.cs
  GameData/ProjectPluto/Parts/Pluto.cfg
  GameData/ProjectPluto/Parts/Assets/PlutoExterior.mu, PlutoSkin.png
```

---

### Task 1: Project scaffold, test runner, constants

**Files:**
- Create: `build-mono.sh`, `run-tests.sh`, `.gitignore`, `Source/PlutoAssembly.cs`,
  `Source/Core/PlutoConstants.cs`, `Tests/TestRunner.cs`, `Tests/ConstantsTests.cs`

**Interfaces:**
- Produces: `static class PlutoConstants` with `const double` fields named below;
  `Tests/TestRunner.cs` with `static class Assert { Equal(double expected, double actual, double tol, string name); True(bool, string name); }`
  and `Main` that reflects over every `public static void` method in classes ending `Tests`, runs
  each, prints `N passed, M failed` and exits non-zero on any failure.

- [ ] **Step 1:** `git init` in the project root; `.gitignore` with `Tests.exe`, `*.zip`, `.DS_Store`.
- [ ] **Step 2:** `build-mono.sh` copied from Sundial's, changed to `ProjectPluto.dll`, compiling
  `Source/*.cs Source/Core/*.cs Source/Effect/*.cs` (skip missing globs with `shopt -s nullglob`).
  `run-tests.sh`: `mcs -out:Tests.exe Source/Core/*.cs Tests/*.cs && mono Tests.exe`.
  `PlutoAssembly.cs`: `AssemblyVersion("0.1.0.0")`, `KSPAssembly("ProjectPluto", 0, 1)`.
- [ ] **Step 3: Write failing test** `ConstantsTests.DoseConstantMatchesVideoPass`:
  `Assert.Equal(0.28, PlutoConstants.DoseK * Math.PI / (150 * 1020), 0.01, ...)`.
- [ ] **Step 4:** Run `./run-tests.sh` → fails to compile (`PlutoConstants` missing).
- [ ] **Step 5:** Create `PlutoConstants` with: `ReactorMaxMW = 600`, `CoreTempFullC = 1277`,
  `AmbientC = 20`, `ScramTempC = 1500`, `WarmupSeconds = 20`, `TempTimeConstant = 10`,
  `DoseK = 13600` (Sv·m²/s at 600 MW), `DoseWarn1 = 0.1`, `DoseWarn2 = 1`, `DoseLethal = 6`,
  `BreachPowerThreshold = 0.1`, `BreachStrength = 0.1`, `BreachSeconds = 600`,
  `WarheadCount = 16`, `WarheadMassT = 0.5`, `AutoReleaseSeconds = 60`, `TossUpMs = 30`,
  `WarheadArmSeconds = 2`, `WarheadSafetySeconds = 120`, `FireballRadiusM = 280`,
  `CloudTopM = 3000`, `DamageRadiusM = 500`, `RunHeightM = 150`, `RunHeightMinM = 100`,
  `RunMach = 3`, `LookaheadStepM = 250`, `LookaheadRangeM = 5000`, `MaxG = 6`,
  `MaxClimbDeg = 30`, `MaxDiveDeg = 15`, `RunMaxEngageAltM = 20000`.
- [ ] **Step 6:** `./run-tests.sh` → `1 passed, 0 failed`. `./build-mono.sh "<Managed>"` → DLL written.
- [ ] **Step 7:** Commit `chore: scaffold ProjectPluto with test runner and constants`.

### Task 2: Reactor model

**Files:** Create `Source/Core/PlutoReactorModel.cs`, `Tests/ReactorTests.cs`

**Interfaces:**
- Produces: `enum PlutoReactorState { Cold, Warming, Running, Scrammed, Breached }`;
  `sealed class PlutoReactorModel { State; double PowerFraction (0..1); double PowerMW; double CoreTempC;
  void Start(); void Scram(); void Restart(); void Breach();
  void Update(double dt, double demand /*0..1 throttle*/, double airflowFraction /*0..1*/);
  string Save(); static PlutoReactorModel Load(string); }`

Behaviour: `Start` from Cold → Warming; power rises linearly to `demand` over `WarmupSeconds` (from 0
to full), then Running. Core temperature relaxes toward
`AmbientC + (CoreTempFullC − AmbientC) × PowerFraction / max(airflowFraction, 0.05)` with time
constant `TempTimeConstant`. Above `ScramTempC` → Scrammed, power drops to 0. Restart from Scrammed
only when `CoreTempC < 800`. Scrammed temperature relaxes to ambient.

- [ ] **Step 1: Failing tests:** `Reactor_WarmsToFullIn20s` (Start, 20 s of 0.1 s steps at demand 1,
  airflow 1 → PowerMW 600 ±1, State Running); `Reactor_FullPowerTempIs1277` (then 120 s more →
  CoreTempC 1277 ±5); `Reactor_NoAirflowScrams` (Running, airflow 0 → Scrammed within 60 s, power 0);
  `Reactor_RestartNeedsCooldown` (Restart right after scram keeps Scrammed; after 300 s, Restart → Warming);
  `Reactor_SaveLoadRoundTrip` (state, power, temp equal after `Load(Save())`).
- [ ] **Step 2:** `./run-tests.sh` → compile failure.
- [ ] **Step 3:** Implement `PlutoReactorModel`; `Save` as `state;power;temp` with invariant culture.
- [ ] **Step 4:** `./run-tests.sh` → all pass.
- [ ] **Step 5:** Commit `feat: reactor state model`.

### Task 3: Radiation maths

**Files:** Create `Source/Core/PlutoRadiation.cs`, `Tests/RadiationTests.cs`

**Interfaces:**
- Produces: `static class PlutoRadiation { double DoseRate(double powerMW, double distanceM);
  enum Threshold { None, Warn1, Warn2, Lethal }; Threshold Crossed(double before, double after); }`
  `DoseRate = DoseK × (powerMW / ReactorMaxMW) / max(distance², 1)`. `Crossed` returns the highest
  threshold passed between the two totals.

- [ ] **Step 1: Failing tests:** `Radiation_VideoPassIs028Sv` (numerically integrate `DoseRate(600, sqrt(150² + (1020t)²))`
  over t ∈ [−60, 60] in 0.001 s steps → 0.28 ±0.01); `Radiation_ScalesWithPower` (300 MW is half of
  600 MW at 500 m); `Radiation_CrossedPicksHighest` (`Crossed(0.05, 7)` → Lethal; `Crossed(0.2, 0.5)` → None).
- [ ] **Step 2:** run → fail. **Step 3:** implement. **Step 4:** run → pass.
- [ ] **Step 5:** Commit `feat: radiation dose maths`.

### Task 4: Ballistics and toss release

**Files:** Create `Source/Core/PlutoBallistics.cs`, `Tests/BallisticsTests.cs`

**Interfaces:**
- Produces: `struct PlutoVec { double X, Y, Z; operators +, −, ×scalar; Length; }`;
  `sealed class PlutoBallistics` with
  `static void Step(ref PlutoVec pos, ref PlutoVec vel, PlutoVec gravity, double dragPerM, double dt)`
  (drag accel = −dragPerM × |v| × v);
  `static PlutoVec PredictImpact(PlutoVec pos, PlutoVec vel, PlutoVec gravity, double dragPerM,
  Func<PlutoVec, double> heightAboveGround, double dt = 0.05, double maxT = 120)`;
  `sealed class PlutoTossTrigger { bool ShouldRelease(PlutoVec predictedImpact, PlutoVec target, PlutoVec trackDir); }`
  returning true on the first call where the along-track distance `(target − impact)·trackDir`
  changes sign from positive to ≤ 0 and the cross-track miss is under 400 m;
  `sealed class PlutoWarheadFuse { bool Update(double dt, double heightAboveGround); }` true when
  armed (age ≥ `WarheadArmSeconds`) and height ≤ 0, or age ≥ `WarheadSafetySeconds`.

Positions are body-relative (origin at the body centre, or a local flat frame in tests); callers in
KSP convert with `body.position` each frame, so origin shifts never touch stored state.

- [ ] **Step 1: Failing tests:** `Ballistics_FlatGroundNoDragMatchesAnalytic` (from 150 m, v = (1020, 30, 0),
  g = (0, −9.81, 0), flat ground → impact x within 1% of analytic `1020 × t` with `t` from the quadratic);
  `Ballistics_TossTriggerFiresOncePastTarget` (impacts marching along x past a target at x = 5000 →
  true exactly once, at the first impact ≥ 5000);
  `Ballistics_TossTriggerIgnoresWideMiss` (cross-track 1 km → never true);
  `Ballistics_ArmingDelayPreventsImmediateDetonation` (fuse with height −1 at age 0.5 s → false; at
  2.1 s → true); `Ballistics_SafetyTimerDetonates` (height 500 at age 120 → true);
  `Ballistics_BodyRelativeStateUnchangedByFrameShift` (stepping a state, then computing world =
  bodyPos + state for two different bodyPos values, gives identical body-relative state).
- [ ] **Step 2:** run → fail. **Step 3:** implement (semi-implicit Euler). **Step 4:** run → pass.
- [ ] **Step 5:** Commit `feat: warhead ballistics, fuse and toss trigger`.

### Task 5: Bombing-run logic

**Files:** Create `Source/Core/PlutoRunLogic.cs`, `Tests/RunLogicTests.cs`

**Interfaces:**
- Produces: `sealed class PlutoPid { PlutoPid(double kp, double ki, double kd, double min, double max);
  double Update(double error, double dt); void Reset(); }`;
  `static class PlutoTerrainLookahead { static double DesiredAltitudeAsl(IList<double> terrainAheadAsl,
  double runHeight, double stepM, double maxClimbDeg); }` (sample `i` lies `(i + 1) × stepM` ahead) returning the lowest ASL altitude that clears every sample by
  `runHeight` given a climb no steeper than `maxClimbDeg` from now to that sample (water samples are
  passed as 0);
  `enum PlutoRunPhase { Idle, Dive, Follow, Done, Aborted }`;
  `sealed class PlutoRunController { Phase; string Message;
  bool TryEngage(double altitudeAsl, double atmosphereTop, int warheadsLeft);
  void Update(double dt, double agl, double desiredAsl, double altitudeAsl, bool manualInput,
  bool reactorRunning, int warheadsLeft);
  double TargetFlightPathDeg { get; } }`
  Dive → Follow when `|agl − runHeight| < 30`. Any `manualInput`, `!reactorRunning`, or
  altitude above atmosphere → Aborted with message. `warheadsLeft == 0` → Done.
  `TargetFlightPathDeg` clamped to [−MaxDiveDeg, +MaxClimbDeg].

- [ ] **Step 1: Failing tests:** `Lookahead_ClimbsEarlyForRidge` (flat 0 m then a 600 m ridge at
  3 km, run height 150 → result ≥ 750 − 3000 × tan(30°) and > 150); `Lookahead_FlatGroundHoldsRunHeight` (all 0 → 150);
  `Pid_ConvergesOnStep` (first-order plant reaches 95% in < 20 s, no output beyond limits);
  `Run_EngageThenDiveThenFollow`; `Run_ManualInputAborts`; `Run_ScramAborts`;
  `Run_DoneWhenWarheadsGone`; `Run_RefusesAboveAtmosphere` (`TryEngage(75000, 70000, 16)` → false,
  message set); `Run_RefusesWithNoWarheads`; `Run_NoTargetHoldsHeading` (controller exposes no
  heading demand when target null — tested on `ModulePlutoAutopilot` helper
  `static double? HeadingToTarget(bool hasTarget, double bearing)` returning null without target).
- [ ] **Step 2:** run → fail. **Step 3:** implement. **Step 4:** run → pass.
- [ ] **Step 5:** Commit `feat: bombing-run logic, lookahead and PID`.

### Task 6: Model and texture

**Files:** Create `Source/BuildPluto.py`, `GameData/ProjectPluto/Parts/Assets/PlutoExterior.mu`,
`PlutoSkin.png`; `tools/io_object_mu/` (downloaded, gitignored)

**Interfaces:**
- Produces transforms (exact names, used by Tasks 7–10): `model` root; `Body`; `Booster1`,
  `Booster2`, `Booster3` (each with its own collider child `Booster1Col`… and a child `boostThrust`);
  `ramjetThrust` (at nozzle, pointing aft); `Canard1`..`Canard3` (hinged at their root); fins
  `Fin1`..`Fin3`; `Intake` (ventral scoop, forward facing, used as intake transform);
  `warheadHatch` (top, mid-body). Y axis is the missile's long axis, nose +Y, as in Sundial.

- [ ] **Step 1:** **Ask the user for OK**, then clone `https://github.com/taniwha/io_object_mu` into
  `tools/io_object_mu` and confirm `mu.py` imports (`python3 -I -c "import sys; sys.path.insert(0,'tools/io_object_mu'); import mu"`
  from a script outside that directory, per the untrusted-download rule). Skim `mu.py` before running it.
- [ ] **Step 2:** Write `BuildPluto.py` reusing Sundial's `add_lathe` approach: body profile
  26.8 m × 0.75 m radius with an ogive nose and slight boat-tail; texture with gold forward third,
  steel elsewhere, 16 hatch outlines in a row on top, "PLUTO" / "SLAM" markings.
- [ ] **Step 3:** Run `python3 Source/BuildPluto.py GameData/ProjectPluto/Parts/Assets <font.ttf>`
  (font from `/System/Library/Fonts`). Verify by reading the `.mu` back with `mu.py` and printing all
  transform names: every name in the Interfaces list present.
- [ ] **Step 4:** Commit `feat: Pluto model and texture`.

### Task 7: Part config and booster jettison

**Files:** Create `GameData/ProjectPluto/Parts/Pluto.cfg`, `Source/ModulePlutoBoosters.cs`

**Interfaces:**
- Consumes: transforms from Task 6.
- Produces: `ModulePlutoBoosters : PartModule, IPartMassModifier, IMultipleDragCube` with
  `[KSPField(isPersistant)] bool boostersJettisoned`, `[KSPField] float boosterDryMass = 3`,
  `[KSPEvent] JettisonBoosters()` + action; drag cube names `Boosted`, `Clean`.

Config values (from spec; tunable within its targets): mass 19.5 t dry + `boosterDryMass` 3 t;
resources SolidFuel 1700, ElectricCharge 200, IntakeAir 10; `ModuleCommand` (minimumCrew 0),
`ModuleSAS`, `ModuleReactionWheel` (10 kN·m each axis), `ModuleControlSurface` ×3 on `Canard1..3`
(ctrlSurfaceRange 15), `ModuleLiftingSurface` (deflectionLiftCoeff 1.5), `ModuleResourceIntake` on
`Intake`; `ModuleEnginesFX engineID = Boost` (SolidFuel, maxThrust 1500, Isp 240/220, throttleLocked,
thrust via `boostThrust`, staged); `ModuleEnginesFX engineID = Ramjet` (IntakeAir only, maxThrust 170,
`velCurve` keys Mach 0:0, 0.8:0.05, 1.5:0.4, 3:1, 4.2:0.9, 5.5:0.2, 6.5:0, `atmCurve` cutting out as
density falls, gimbal 3° on `ramjetThrust`, not staged); `ModuleGimbal` for the ramjet. Description
text per spec "Text" section.

- [ ] **Step 1:** Write `Pluto.cfg` and `ModulePlutoBoosters`: on `FixedUpdate`, when not jettisoned
  and the Boost engine has flamed out with SolidFuel ≈ 0 → hide `Booster1..3`, disable their
  colliders, spawn three debris clones (renderers only, rigidbody with outward 8 m/s + aft push,
  destroyed on collision or after 60 s), set drag cube to `Clean`, set `boostersJettisoned`. (The
  hand-over call to the reactor is added in Task 8.)
  `GetModuleMass` returns `−boosterDryMass` when jettisoned. `OnStart` re-applies hidden state.
- [ ] **Step 2:** `./build-mono.sh "<Managed>"` → no errors. Copy `GameData/ProjectPluto` into the
  KSP GameData folder.
- [ ] **Step 3: In-game check (user or computer use with the user's OK):** (1) part shows in VAB
  under Engine with the right look; (2) staged on a ramp at 40°, boosters fire; (3) at burnout they
  drop off as debris, mass falls by about 3 t and booster thrust stops; (4) quicksave after
  jettison, reload → boosters still gone, mass correct; (5) burnout speed ≥ Mach 2 and altitude
  ≥ 3 km — if not, tune SolidFuel / maxThrust; (6) reload mid-boost resumes correctly.
- [ ] **Step 4:** Commit `feat: part config and auto booster jettison`.

### Task 8: Reactor module and radiation scenario

**Files:** Create `Source/ModulePlutoReactor.cs`, `Source/PlutoRadiationScenario.cs`;
Modify `Source/ModulePlutoBoosters.cs` (call `OnBoostersGone`), `Pluto.cfg` (add MODULE + SCENARIO).

**Interfaces:**
- Consumes: `PlutoReactorModel`, `PlutoRadiation`, `PlutoConstants`.
- Produces: `ModulePlutoReactor : PartModule` with `[KSPField(isPersistant)] string reactorSave`,
  `PlutoReactorModel Model { get; }`, `bool IsRunning`, `void OnBoostersGone()` (ensures Ramjet engine
  is activated), `[KSPEvent] Scram()`, `Restart()` + actions; PAW readouts State, Power (MW),
  Core temperature (°C), Airflow (%), Crew dose rate here.
  `PlutoRadiationScenario : ScenarioModule` (`[KSPScenario(ScenarioCreationOptions.AddToAllGames,
  GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION)]`) with
  `static PlutoRadiationScenario Instance`, `void AddDose(ProtoCrewMember k, double sv)`,
  `double DoseOf(string kerbalName)`, `void AddBreach(CelestialBody b, double lat, double lon,
  double alt, double expiresUT)`; persists doses and breaches in its node.

Behaviour: reactor `Start()` on Boost engine ignition (spec "Reactor timing"). Each `FixedUpdate`:
`Model.Update(dt, mainThrottle, airflowFraction)` where airflowFraction = Ramjet engine's
intake-air satisfaction (`engine.propellants[0].isDeprived` → 0, otherwise ratio of
`requirementMet`); Ramjet `thrustPercentage` = `PowerFraction × 100`; produce
ElectricCharge 5/s while running. Dose: for every loaded vessel on the same body, every crew member
(and EVA kerbals) gets `DoseRate(PowerMW, distance) × dt`; scenario posts messages at thresholds and
kills at lethal (`ProtoCrewMember.Die()` then remove from part via `part.RemoveCrewmember` /
EVA vessel `Die()`). `OnPartDie` / `part.OnJustAboutToBeDestroyed`: if `PowerFraction >
BreachPowerThreshold` → `Breach()`, spawn a cheap plume, `AddBreach(...)` at
`BreachStrength` for `BreachSeconds`. Scenario doses kerbals near active breaches each FixedUpdate.

- [ ] **Step 1:** Implement both files and the cfg changes.
- [ ] **Step 2:** `./run-tests.sh` (still all pass) and `./build-mono.sh` → no errors; install.
- [ ] **Step 3: In-game check:** warm-up runs during boost and reaches 600 MW near burnout; thrust
  continues smoothly after jettison; level flight at 150 m reaches about Mach 3 with ≤ 5° AoA and at
  9 km about Mach 4 (tune `maxThrust`, `velCurve`, lift coefficient if not); SCRAM and Restart work;
  climbing out of the atmosphere SCRAMs; a kerbal on EVA under a 150 m Mach 3 pass gets 0.25–0.31 Sv;
  crashing at power leaves a plume and a dose zone that survives quicksave/reload.
- [ ] **Step 4:** Commit `feat: reactor module and radiation scenario`.

### Task 9: Scaled detonation effect

**Files:** Create `Source/Effect/Pluto*.cs` copied from Sundial's `SundialDetonationEffect`,
`SundialCloudMesh`, `SundialFireballGlow`, `SundialShockFront`, `SundialHeatHaze`,
`SundialBurstQuads`, `SundialShockShake`, `SundialCameraZoom`, `SundialCameraHold` (renamed
`Pluto…`); do not copy `SundialCage` or `ModuleSundialPayload`.

**Interfaces:**
- Produces: `PlutoDetonationEffect.Spawn(Vector3 center, CelestialBody body, float sizeScale)` where
  `sizeScale = 1` reproduces Sundial's look and Pluto passes
  `FireballRadiusM / 2800` (= 0.1). Every hard-coded distance constant in the effect
  (`KerbinCloudTop`, `EarlyCloudTop`, `EarlyCapRadius`, shock range, vacuum sizes, fireball and visual
  radii) is multiplied by `sizeScale`; Pluto's cloud top cap is `CloudTopM`. Puff counts are
  multiplied by a `quality` factor of 0.35 for Pluto.
  `static void PlutoBlast.Apply(Vector3 center, CelestialBody body, float damageRadius, Part source)`
  destroys loaded parts within radius; Pluto parts hit call `ModulePlutoReactor.Breach` path and are
  destroyed; warheads aboard do not detonate (spec "Blasts hitting a Pluto").

- [ ] **Step 1:** Copy and rename; `grep -n Sundial Source/Effect` → no matches. Refactor constants
  into the scale. Check effect objects anchor to the body (follow floating origin) — if Sundial's do
  not, parent them to a transform repositioned from a body-relative double position each frame.
- [ ] **Step 2:** Add a temporary debug PAW event "Test detonation (debug)" on the reactor module,
  build, install.
- [ ] **Step 3: In-game check:** a test burst shows a fireball ~280 m radius and cloud ~3 km tall; two
  bursts 60 s apart stay playable (note FPS); Sundial installed at the same time still works and its
  own burst is unchanged.
- [ ] **Step 4:** Remove the debug event. Commit `feat: scaled 1 Mt detonation effect`.

### Task 10: Warheads

**Files:** Create `Source/ModulePlutoWarheads.cs`, `Source/PlutoWarheadProjectile.cs`; Modify `Pluto.cfg`.

**Interfaces:**
- Consumes: `PlutoBallistics`, `PlutoWarheadFuse`, `PlutoTossTrigger`, `PlutoDetonationEffect`, `PlutoBlast`.
- Produces: `ModulePlutoWarheads : PartModule, IPartMassModifier` with
  `[KSPField(isPersistant)] int warheadsLeft = 16`, `[KSPField(isPersistant)] bool autoRelease`,
  `[KSPField] float autoReleaseSeconds = 60`, `[KSPField(isPersistant)] bool releaseOnTarget`,
  `bool ReleaseWarhead()` (public, used by autopilot), `PlutoVec PredictImpactBodyRelative()`
  (current release prediction), KSPEvents "Release warhead", toggles for auto-release and release on
  target, action groups. `GetModuleMass` = `−(16 − warheadsLeft) × WarheadMassT`.
  `PlutoWarheadProjectile : MonoBehaviour` holding body-relative `PlutoVec pos, vel`, a small mesh,
  stepping in `FixedUpdate` with `PlutoBallistics.Step` (gravity from `body.gravParameter`, drag from
  atmospheric density), placing its transform at `body.position + pos` each frame, and on fuse →
  `PlutoDetonationEffect.Spawn` + `PlutoBlast.Apply`. Height above ground from
  `body.TerrainAltitude(lat, lon)` (ocean → 0). Destroyed on scene change.

- [ ] **Step 1:** Implement; release = missile velocity + `TossUpMs` along local up, spawned at
  `warheadHatch`.
- [ ] **Step 2:** `./run-tests.sh`, `./build-mono.sh`, install.
- [ ] **Step 3: In-game check:** manual release throws a warhead that lands and detonates; 16 then
  the button stops; mass drops 0.5 t each; auto-release every 60 s; switching camera away and
  back does not move the falling warhead; quicksave/reload keeps the count; release at low speed
  still detonates; the missile 11 km away is unharmed.
- [ ] **Step 4:** Commit `feat: tossed warheads`.

### Task 11: Bombing-run autopilot

**Files:** Create `Source/ModulePlutoAutopilot.cs`; Modify `Pluto.cfg`.

**Interfaces:**
- Consumes: `PlutoRunController`, `PlutoTerrainLookahead`, `PlutoPid`, `ModulePlutoWarheads`,
  `ModulePlutoReactor`.
- Produces: `ModulePlutoAutopilot : PartModule` with KSPEvents "Commence bombing run" and "Abort
  bombing run", action group for Commence, PAW fields run height (`UI_FloatRange` 100–500 m, default
  150), run speed (Mach 2–4, default 3), status text; `static double? HeadingToTarget(bool hasTarget,
  double bearing)`.

Behaviour: nothing is attached to `vessel.OnFlyByWire` until Commence succeeds (spec: manual control
until the run). Each frame: sample terrain ASL every `LookaheadStepM` to `LookaheadRangeM` along the
ground track, `DesiredAltitudeAsl` → flight-path demand → pitch PID → `ctrlState.pitch`; bank toward
`HeadingToTarget` (vessel target or `NavWaypoint` active) with roll PID, max bank set so turn load ≤
`MaxG`; throttle PID on Mach. Manual input detected as non-zero `FlightInputHandler.state` pitch/yaw/
roll from the player → Aborted. In the run, warheads release via `autoRelease` timer or the toss
trigger when `releaseOnTarget`. On Done/Aborted: level wings and pitch for 2 s, then detach from
`OnFlyByWire` and post the message.

- [ ] **Step 1:** Implement; `./run-tests.sh` (includes `Run_NoTargetHoldsHeading` against the static helper), build, install.
- [ ] **Step 2: In-game check:** nothing changes control before Commence; Commence at 9 km dives to
  150 m; holds 100–250 m AGL across the highlands west of KSC without hitting ground; flies to a
  planted flag and with "Release on target" lands a warhead within ~400 m of it; touching the controls
  aborts at once; reactor SCRAM aborts; run ends after the 16th warhead with control returned in level
  flight; Commence above 70 km is refused with a message.
- [ ] **Step 3:** Commit `feat: bombing-run autopilot`.

### Task 12: README, packaging, final check

**Files:** Create `README.md`; produce `dist/ProjectPluto-v0.1.0.zip`.

- [ ] **Step 1:** README in Sundial's style: history with the researched numbers and their sources
  (spec "Real specifications" table and its links), disagreements, the list of invented values
  (booster count, warm-up timing, radiation formula and 6 Sv threshold, warhead toss, 1 Mt effect
  sizes, autopilot), install steps, how to fly a mission, building.
- [ ] **Step 2:** Zip `GameData/ProjectPluto` (with DLL), `Source`, `Tests`, scripts, README.
- [ ] **Step 3:** Run the full spec Verification checklist once in game with Sundial installed.
- [ ] **Step 4:** Commit `docs: README and v0.1.0 package`.
