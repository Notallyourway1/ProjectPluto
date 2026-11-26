# Project Pluto SLAM v0.2.0

A standalone mod for Kerbal Space Program 1.12.x: the **SLAM** (Supersonic Low Altitude Missile) of
Project Pluto, a nuclear ramjet cruise missile, as one part with its boosters, reactor, warheads, probe
core and a bombing-run autopilot built in, plus a ground launcher to fire it from. No other mods or DLC
are needed.

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
| Shape | Wingless; long cone nose with a terrain-avoidance antenna probe; three small all-moving control fins on the nose; ventral intake pod with a centre cone; warhead bay on top amidships; three swept tail fins; gold-plated forward sections to radiate heat |
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
- **Engine physics:** thrust comes from an ideal-ramjet model: air captured by the 1.4 m² mouth of
  the intake pod (about 1,750 kg/s at Mach 3 at sea level; Tory II-C took 910 kg/s), heated by the
  reactor to no hotter than the 1,277 °C core, and expelled faster than it entered, with 85% inlet and
  nozzle efficiency. KSP's drag is far higher than a real missile's, so that thrust is multiplied by
  1.5 (`thrustMultiplier` in the part config). The result is about 190 kN at Mach 0.8 and 440 kN at
  Mach 3 at sea level (Tory II-C made about 170 kN at a simulated Mach 2.8), nothing at rest, less in
  thin air, and no thrust near Mach 4.8 at sea level (higher in colder air), where ram heating alone
  brings the air to core temperature. Other planets' atmospheres use their own gas properties.
- **Reactor governor and SCRAM:** power is limited to the heat the airflow can carry away. Power
  rises at 5% a second but control rods can cut it at up to 500% a second, so a sudden loss of air
  (inlet unstart, booster burnout) is ridden out; the core still shuts down (SCRAM) above 1,500 °C.
- **Airframe heat:** 0.2% of reactor power leaks into the structure, plus 50 kW per °C the core runs
  above design temperature. KSP's heat system takes it from there. At normal operation this is a
  small effect next to aerodynamic heating at Mach 3.
- **Radioactive exhaust trail:** fission fragments in the exhaust leave a haze that starts as a thin
  pale streak behind the nozzle, spreads to about 70 m wide and fades to blue-grey as it ages. Each
  second of flight leaves a source of 0.5% of reactor power that decays over about a minute and is
  gone after five, dosing kerbals underneath.
- **Inlet unstart:** above Mach 4.5 the scoop unstarts and captures only 20% of its air, so thrust
  and cooling collapse until the missile stays below Mach 4.0 for 2 s.
- **Exhaust visuals:** the nozzle glows from dull red to orange with exhaust temperature, and the
  exhaust shows faint red shock diamonds. With **Waterfall** and **Stock Waterfall Effects**
  installed, an optional patch uses their afterburning-ramjet plume (the one they give the Whiplash):
  heat shimmer and shock diamonds that grow with speed. Nothing else is needed without them.
- **Radiation:** dose rate = 13,600 × (power / 600 MW) / distance² Sv/s, chosen so a Mach 3 pass
  150 m overhead gives about 0.28 Sv. Warnings at 0.1 and 1 Sv; **6 Sv is lethal**. A reactor
  destroyed while hot leaves a radiation source at 10% strength for 10 minutes.
- **Warheads:** 16 × 0.5 t, tossed up at 30 m/s and back at 10 m/s, armed after 2 s, detonating on
  impact (or after 120 s). A screen warning appears if a release is slow or low enough that the
  missile will still be inside the blast. Each burst is a 1 Mt effect scaled for Kerbin: about 280 m fireball radius, 3 km cloud,
  500 m damage radius. A nearby blast does not set off another missile's warheads.
- **The bombing-run autopilot.**
- **The launcher:** a cradle and tower that tilts the missile to a set elevation and holds it until
  launch. The real SLAM's launch arrangement was never settled.
- The model's details. Its shape and paint follow Vought's 1/10 scale model and its cutaway drawing
  (copper forward body, white tail section, USAF star-and-bar insignia), but exact fin sizes, booster
  shape and positions are estimates from photos.

## Install on macOS

1. Quit KSP and unzip the archive.
2. Copy `GameData/ProjectPluto` into your KSP `GameData` folder. The DLL must end up at
   `GameData/ProjectPluto/Plugins/ProjectPluto.dll`.
3. Start KSP. Two parts appear (search "pluto"); Career and Science modes unlock both with
   **Nuclear Propulsion**:
   - **Project Pluto SLAM** under **Command**: the missile. It is its own probe core with a built-in
     antenna, reaction wheels and SAS, so it needs no other part.
   - **Project Pluto Launcher** under **Structural**: the ground launcher.

## Fly a mission

1. **Build and launch.** In the VAB, start a new craft with the **Project Pluto SLAM**. Attach the
   **Project Pluto Launcher** under its belly (the side with the air scoop). The craft tilts to the
   launcher's **Launch elevation** (default 45°, 15–90°; changing it re-tilts the craft), and its
   tower reaches below the missile's tail. The missile flies toward the side the belly faced before
   tilting, so turn the missile before attaching the launcher to choose the direction.
   Check the staging list: the launcher and the boosters should fire in the same stage. Launch, and
   the tower stands on the ground and holds the missile still. Stage once: the boosters fire, the
   missile slides off the cradle, the launcher stays behind, and the reactor starts warming up.
2. **Booster burnout.** The boosters drop off by themselves, the reactor is at full power and the
   ramjet takes over. The right-click menu shows reactor state, power, core temperature and cooling
   airflow. You can **SCRAM** and **Restart** the reactor (restart needs the core below 800 °C).
3. **Fly it yourself** for as long as you like. The ramjet breathes air only: it is weak below
   Mach 0.7, is strongest low down around Mach 3, fades in thin air, and stops above the atmosphere.
   The right-click menu adds ramjet thrust, exhaust temperature and inlet state. Keep below Mach 4.5 or the inlet
   unstarts.
4. **Pick a target** (any craft, a planted flag, or an active navigation waypoint), or none to fly a
   straight line.
5. **Commence bombing run** (right-click menu or an action group). The missile dives to the run height
   (default 150 m, 100–500 m), holds it over the terrain at the run speed (default Mach 3), and
   releases warheads:
   - with **Release on target** on and a target selected, a warhead is thrown so it lands on the target;
   - otherwise (no target needed) one every **Release interval** (default 60 s, 2–120 s, adjustable in
     flight), the line of strikes from the "What if" video. At Mach 3, 10 s is about 10 km apart.
   Releases that would leave the missile inside its own blast (too slow or too low) are held until safe.
6. Touching pitch, yaw or roll **aborts the run** and gives you control at once. The run also stops if
   the reactor SCRAMs or the missile leaves the atmosphere. When the last warhead is gone it levels
   off and hands control back.

Without a run or a target you can release warheads by hand at any time (**Release warhead**, also an action group), or switch on **Auto-release** to drop one every **Release interval**.

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
- [SLAM photographs](https://vought.org/photo/html/pslam.html) (1/10 scale model and cutaway), Vought Heritage: the model's shape and paint
- [Tory IIC reactor test report](https://www.osti.gov/biblio/4333097) and [Tory IIC test operations](https://www.osti.gov/biblio/4356168-tory-iic-test-operations), OSTI
- [Project Pluto: The Nuclear Crowbar](https://scalar.chapman.edu/scalar/project-pluto-the-nuclear-crowbar/slams), Chapman University
- Detonation visuals adapted from the author's Project Sundial mod.
- taniwha, [io_object_mu](https://github.com/taniwha/io_object_mu) (GPL-2.0), used as a submodule to
  write the KSP model.
