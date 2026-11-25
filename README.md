# Project Pluto SLAM v0.1.0

A standalone part for Kerbal Space Program 1.12.x: the **SLAM** (Supersonic Low Altitude Missile) of
Project Pluto, a nuclear ramjet cruise missile, as one part with its boosters, reactor, warheads and a
bombing-run autopilot built in. No other mods or DLC are needed.

> **Status:** compiled and unit-tested, **not yet tested in game**. Expect tuning changes after the
> first flights.

## Historical background

Project Pluto (1957–1964) was a U.S. Air Force and Livermore programme to build a cruise missile
driven by a nuclear ramjet: air rammed through an unshielded reactor core, heated, and blasted out the
back, with no fuel to burn. The missile was SLAM. Only the engine was ever built.

| | Real value |
|---|---|
| Length / body diameter | 26.8 m / 1.5 m |
| Launch mass | 27,540 kg |
| Shape | Wingless; three tail fins, small canards near the nose, belly air scoop; gold-plated forward sections to radiate heat |
| Speed | Mach 3–3.5 at 150–300 m; Mach 4.2 at about 9 km |
| Range | 21,300 km low; 182,000 km high |
| Reactor | 600 MW thermal, unshielded; 465,000 ceramic fuel tubes; about 1,277 °C |
| Warheads | 14 to 26 depending on source (usually given as 16), tossed from hatches on top |
| Tory II-C engine test | Full power on 20 May 1964 for 292 s at about 461–513 MW, about 910 kg/s of air, about 170 kN thrust |
| Cancelled | 1 July 1964 |

Sources disagree on a few points, kept as they are here: low-level flight height (150 m or 300 m),
the warhead count, Tory II-C's power (461–513 MW), and the date Tory II-A first reached full power
(14 May or 5 October 1961).

### What this mod invents

These are gameplay choices, not history:

- **Three integrated boosters** (sources say "several" without a number), 1,500 kN for about 20 s.
- **Reactor timing:** it warms up from booster ignition and reaches full power at burnout. The real
  reactor was only to be switched on at cruise altitude far from people.
- **Reactor governor and SCRAM:** power is limited to what the airflow can cool; the core shuts down
  (SCRAM) above 1,500 °C.
- **Radiation:** dose rate = 13,600 × (power / 600 MW) / distance² Sv/s, chosen so a Mach 3 pass
  150 m overhead gives about 0.28 Sv. Warnings at 0.1 and 1 Sv; **6 Sv is lethal**. A reactor
  destroyed while hot leaves a radiation source at 10% strength for 10 minutes.
- **Warheads:** 16 × 0.5 t, tossed up at 30 m/s and back at 10 m/s, armed after 2 s, detonating on
  impact (or after 120 s). A screen warning appears if a release is slow or low enough that the
  missile will still be inside the blast. Each burst is a 1 Mt effect scaled for Kerbin: about 280 m fireball radius, 3 km cloud,
  500 m damage radius. A nearby blast does not set off another missile's warheads.
- **The bombing-run autopilot.**
- The model's details (canard and fin sizes, booster shape, markings).

## Install on macOS

1. Quit KSP and unzip the archive.
2. Copy `GameData/ProjectPluto` into your KSP `GameData` folder. The DLL must end up at
   `GameData/ProjectPluto/Plugins/ProjectPluto.dll`.
3. Start KSP. The part is **Project Pluto SLAM** under **Engines** (search "pluto"). Career and Science
   modes unlock it with **Nuclear Propulsion**.

## Fly a mission

1. **Launch.** Put the missile on a ramp or launch clamps, pointed 30–45° up. Stage once: the boosters
   fire and the reactor starts warming up.
2. **Booster burnout.** The boosters drop off by themselves, the reactor is at full power and the
   ramjet takes over. The right-click menu shows reactor state, power, core temperature and cooling
   airflow. You can **SCRAM** and **Restart** the reactor (restart needs the core below 800 °C).
3. **Fly it yourself** for as long as you like. The ramjet breathes air only: it gives almost nothing
   below Mach 1, peaks around Mach 3–4, and stops above the atmosphere.
4. **Pick a target** (any craft, a planted flag, or an active navigation waypoint), or none to fly a
   straight line.
5. **Commence bombing run** (right-click menu or an action group). The missile dives to the run height
   (default 150 m, 100–500 m), holds it over the terrain at the run speed (default Mach 3), and
   releases warheads:
   - with **Release on target** on and a target selected, a warhead is thrown so it lands on the target;
   - otherwise one every 60 s, the line of strikes from the "What if" video.
6. Touching pitch, yaw or roll **aborts the run** and gives you control at once. The run also stops if
   the reactor SCRAMs or the missile leaves the atmosphere. When the last warhead is gone it levels
   off and hands control back.

You can also release warheads by hand at any time (**Release warhead**), or switch on **Auto-release**.

If the autopilot pitches or rolls the wrong way, set `pitchSign = -1` or `rollSign = -1` in the
`ModulePlutoAutopilot` block of `GameData/ProjectPluto/Parts/Pluto.cfg`.

## Building

Requires Mono (`mcs`).

```bash
./run-tests.sh
./build-mono.sh "<KSP folder>/KSP.app/Contents/Resources/Data/Managed"
```

`run-tests.sh` compiles the game-independent maths in `Source/Core` with the tests in `Tests` and runs
them. `build-mono.sh` writes `GameData/ProjectPluto/Plugins/ProjectPluto.dll`.

The model is generated, not hand-made:

```bash
git submodule update --init
python3 -I Source/BuildPluto.py tools/io_object_mu GameData/ProjectPluto/Parts/Assets "/System/Library/Fonts/Supplemental/Arial Bold.ttf"
```

## Sources and credits

- [Supersonic Low Altitude Missile](https://en.wikipedia.org/wiki/Supersonic_Low_Altitude_Missile), Wikipedia
- [Project Pluto](https://en.wikipedia.org/wiki/Project_Pluto), Wikipedia
- [SLAM](https://designation-systems.net/dusrm/app4/slam.html), Directory of U.S. Military Rockets and Missiles
- [SLAM airframe](https://vought.org/special/html/sslam1.html) and [reactor](https://vought.org/special/html/sslam3.html), Vought Heritage
- [Tory IIC reactor test report](https://www.osti.gov/biblio/4333097) and [Tory IIC test operations](https://www.osti.gov/biblio/4356168-tory-iic-test-operations), OSTI
- [Project Pluto: The Nuclear Crowbar](https://scalar.chapman.edu/scalar/project-pluto-the-nuclear-crowbar/slams), Chapman University
- Detonation visuals adapted from the author's Project Sundial mod.
- taniwha, [io_object_mu](https://github.com/taniwha/io_object_mu) (GPL-2.0), used as a submodule to
  write the KSP model.
