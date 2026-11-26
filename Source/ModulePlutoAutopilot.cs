using System;
using UnityEngine;

namespace ProjectPluto
{
    // Two autopilots. Take-off (a setting, on by default): flies off the launcher, climbs to the cruise
    // height and cruises there. Bombing run: dives to the run height, follows the terrain toward the
    // target, releases the warheads, then hands control back. Any manual input ends either one.
    public sealed class ModulePlutoAutopilot : PartModule
    {
        private const float ManualThreshold = 0.05f;
        private const float LevelOffSeconds = 2f;
        private const int PredictEveryFrames = 5;
        private const int LookaheadEveryFrames = 5;
        // Only predict a toss when the target is this close ahead along the track.
        private const double PredictRangeM = 20000;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Take-off autopilot",
            groupName = PlutoReadout.AutopilotGroup, groupDisplayName = PlutoReadout.AutopilotGroupTitle),
            UI_Toggle(disabledText = "Off", enabledText = "On", scene = UI_Scene.All)]
        public bool takeoffAutopilot = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Cruise height",
            guiUnits = " m", groupName = PlutoReadout.AutopilotGroup, groupDisplayName = PlutoReadout.AutopilotGroupTitle),
            UI_FloatRange(minValue = 300f, maxValue = 5000f, stepIncrement = 100f, scene = UI_Scene.All)]
        public float cruiseHeight = 1000f;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Run height", guiUnits = " m",
            groupName = PlutoReadout.AutopilotGroup, groupDisplayName = PlutoReadout.AutopilotGroupTitle),
            UI_FloatRange(minValue = 100f, maxValue = 500f, stepIncrement = 10f, scene = UI_Scene.Flight)]
        public float runHeight = (float)PlutoConstants.RunHeightM;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Cruise and run speed (Mach)", guiFormat = "F1",
            groupName = PlutoReadout.AutopilotGroup, groupDisplayName = PlutoReadout.AutopilotGroupTitle),
            UI_FloatRange(minValue = 2f, maxValue = 4f, stepIncrement = 0.1f, scene = UI_Scene.Flight)]
        public float runMach = (float)PlutoConstants.RunMach;

        [KSPField(guiActive = true, guiName = "Autopilot", groupName = PlutoReadout.AutopilotGroup, groupDisplayName = PlutoReadout.AutopilotGroupTitle)]
        public string status = "Manual control";

        // Flip these in the part config if a control axis turns out reversed in game.
        [KSPField] public float pitchSign = 1f;
        [KSPField] public float rollSign = 1f;

        // Saved so a reload in the middle of a run can undo what the run changed.
        [KSPField(isPersistant = true)] public bool runActive;
        [KSPField(isPersistant = true)] public bool runOwnsAutoRelease;
        [KSPField(isPersistant = true)] public bool sasWasOn;
        [KSPField(isPersistant = true)] public bool takeoffDone;
        [KSPField(isPersistant = true)] public bool takeoffActive;

        private PlutoRunController run;
        private readonly PlutoTossTrigger toss = new PlutoTossTrigger();
        private PlutoPid pitchPid, rollPid, throttlePid;
        private ModulePlutoWarheads warheads;
        private ModulePlutoReactor reactor;
        private ModulePlutoBoosters boosters;
        private PlutoTakeoffController takeoff;
        private PlutoTakeoffPhase shownTakeoffPhase;
        private bool attached;
        private Vessel subscribed;
        private float levelOffLeft;
        private int frame;
        private double[] samples;
        private double desiredAsl;
        private double groundAsl;

        public override string GetInfo() =>
            "Take-off autopilot (on by default): flies off the launcher, climbs to the cruise height and cruises " +
            "there until you move the controls.\n" +
            "Bombing-run autopilot. Manual control until you choose Commence bombing run; then it dives to " +
            $"{PlutoConstants.RunHeightM:0} m, follows the terrain at Mach {PlutoConstants.RunMach:0} toward the " +
            "selected target and releases the warheads. Any control input aborts the run.";

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            warheads = part.FindModuleImplementing<ModulePlutoWarheads>();
            reactor = part.FindModuleImplementing<ModulePlutoReactor>();
            boosters = part.FindModuleImplementing<ModulePlutoBoosters>();
            if (HighLogic.LoadedSceneIsFlight && takeoffActive)
            {
                if (sasWasOn && vessel != null)
                    vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                takeoffActive = false;
                takeoffDone = true;
                Post("Take-off autopilot ended by reload: manual control.");
            }
            if (HighLogic.LoadedSceneIsFlight && runActive)
            {
                // A run cannot survive a reload: undo what it switched on and hand control back.
                if (runOwnsAutoRelease && warheads != null)
                    warheads.autoRelease = false;
                if (sasWasOn && vessel != null)
                    vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                runActive = runOwnsAutoRelease = false;
                Post("Bombing run ended by reload: manual control.");
            }
            RefreshControls();
        }

        public void OnDestroy() => Detach();

        [KSPEvent(guiActive = true, guiName = "Commence bombing run", groupName = PlutoReadout.AutopilotGroup, groupDisplayName = PlutoReadout.AutopilotGroupTitle)]
        public void CommenceRun()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null || (attached && takeoff == null))
                return;
            if (reactor == null || !reactor.IsRunning)
            {
                Post("Bombing run needs the reactor running.");
                return;
            }

            run = new PlutoRunController(runHeight);
            if (!run.TryEngage(vessel.altitude, vessel.mainBody.atmosphereDepth, warheads != null ? warheads.warheadsLeft : 0))
            {
                Post(run.Message);
                return;
            }

            // The bombing run takes over from the take-off autopilot.
            if (takeoff != null)
                Detach();
            toss.Reset();
            runOwnsAutoRelease = false;
            levelOffLeft = 0;
            Attach();
            runActive = true;
            Post(run.Message);
            RefreshControls();
        }

        [KSPEvent(guiActive = true, guiName = "Abort bombing run", groupName = PlutoReadout.AutopilotGroup, groupDisplayName = PlutoReadout.AutopilotGroupTitle)]
        public void AbortRun()
        {
            if (!attached || takeoff != null || run == null)
                return;
            run.Abort("Bombing run aborted.");
            Finish();
        }

        [KSPAction("Commence bombing run")]
        public void CommenceAction(KSPActionParam param) => CommenceRun();

        [KSPAction("Abort bombing run")]
        public void AbortAction(KSPActionParam param) => AbortRun();

        // Shared start for both autopilots: fresh controllers, SAS off (restored on detach), fly-by-wire on.
        private void Attach()
        {
            pitchPid = new PlutoPid(0.06, 0.02, 0.02, -1, 1);
            rollPid = new PlutoPid(0.03, 0.005, 0.01, -1, 1);
            throttlePid = new PlutoPid(2.0, 0.4, 0, 0, 1);
            frame = 0;
            samples = new double[(int)(PlutoConstants.LookaheadRangeM / PlutoConstants.LookaheadStepM) + 1];
            sasWasOn = vessel.ActionGroups[KSPActionGroup.SAS];
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            vessel.OnFlyByWire += Fly;
            subscribed = vessel;
            attached = true;
        }

        public void FixedUpdate()
        {
            if (takeoff != null && !takeoffAutopilot)
            {
                takeoff.End("Take-off autopilot switched off: manual control.");
                EndTakeoff();
                return;
            }
            if (!takeoffAutopilot || takeoffDone || attached || !HighLogic.LoadedSceneIsFlight || vessel == null
                || vessel.packed || boosters == null || !boosters.BoostEngineIgnited || !MissileIsFree())
                return;
            takeoffDone = true;
            takeoffActive = true;
            takeoff = new PlutoTakeoffController(cruiseHeight);
            takeoff.Start();
            shownTakeoffPhase = takeoff.Phase;
            Attach();
            Post(takeoff.Message);
            RefreshControls();
        }

        // Off the launcher and flying on its own (not part of a carrier craft).
        private bool MissileIsFree()
        {
            if (vessel.rootPart != part)
                return false;
            foreach (Part child in part.children)
            {
                if (child.FindModuleImplementing<ModulePlutoLaunchPad>() != null)
                    return false;
            }
            return true;
        }

        private bool IsManual(FlightCtrlState s) =>
            vessel == FlightGlobals.ActiveVessel && (Mathf.Abs(s.pitch - s.pitchTrim) > ManualThreshold
                || Mathf.Abs(s.yaw - s.yawTrim) > ManualThreshold
                || Mathf.Abs(s.roll - s.rollTrim) > ManualThreshold);

        private void FlyTakeoff(FlightCtrlState s)
        {
            if (vessel != subscribed)
            {
                takeoff.End("Take-off autopilot off: missile changed vessel.");
                EndTakeoff();
                return;
            }
            float dt = TimeWarp.fixedDeltaTime;
            CelestialBody body = vessel.mainBody;
            Vector3d position = vessel.CoMD;
            Vector3d up = (position - body.position).normalized;
            Vector3d velocity = vessel.srf_velocity;
            Vector3d horizontal = Vector3d.Exclude(up, velocity);
            Vector3d track = horizontal.sqrMagnitude > 1 ? horizontal.normalized
                : (Vector3d)Vector3.ProjectOnPlane(vessel.transform.up, up).normalized;
            if (frame++ % LookaheadEveryFrames == 0)
            {
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = TerrainAsl(body, position + track * (i * PlutoConstants.LookaheadStepM));
                desiredAsl = PlutoTerrainLookahead.DesiredAltitudeAsl(samples, takeoff.CruiseHeight,
                    PlutoConstants.LookaheadStepM, PlutoConstants.LookaheadClimbDeg);
            }

            bool boostersGone = boosters == null || boosters.boostersJettisoned;
            bool reactorOk = reactor != null && reactor.Model.IsPowered;
            takeoff.Update(dt, vessel.altitude, desiredAsl, vessel.srfSpeed, IsManual(s), boostersGone, reactorOk,
                vessel.altitude < body.atmosphereDepth);
            if (!takeoff.Flying)
            {
                EndTakeoff();
                return;
            }
            if (takeoff.Steering)
                FlyTo(s, takeoff.TargetFlightPathDeg, null, up, velocity, track, dt);
            s.mainThrottle = takeoff.Phase == PlutoTakeoffPhase.Cruise
                ? (float)throttlePid.Update(runMach - vessel.mach, dt)
                : 1f;

            if (takeoff.Phase != shownTakeoffPhase)
            {
                shownTakeoffPhase = takeoff.Phase;
                if (takeoff.Phase == PlutoTakeoffPhase.Cruise)
                    Post(takeoff.Message);
            }
            status = takeoff.Phase == PlutoTakeoffPhase.ClearRail ? "Take-off: leaving the launcher"
                : takeoff.Phase == PlutoTakeoffPhase.Climb ? "Take-off: climbing"
                : "Cruising";
        }

        private void EndTakeoff()
        {
            Post(takeoff.Message);
            Detach();
        }

        private void Fly(FlightCtrlState s)
        {
            if (vessel == null)
                return;
            if (takeoff != null)
            {
                FlyTakeoff(s);
                return;
            }
            if (run == null)
                return;
            if (vessel != subscribed)
            {
                // The missile separated from the craft it was part of: stop flying the old one.
                run.Abort("Bombing run aborted: missile changed vessel.");
                Finish();
                return;
            }

            // Only the active vessel takes player input; elsewhere the state holds our own last outputs.
            bool manual = IsManual(s);
            float dt = TimeWarp.fixedDeltaTime;
            CelestialBody body = vessel.mainBody;
            Vector3d position = vessel.CoMD;
            Vector3d up = (position - body.position).normalized;
            Vector3d velocity = vessel.srf_velocity;
            Vector3d horizontal = Vector3d.Exclude(up, velocity);
            Vector3d track = horizontal.sqrMagnitude > 1 ? horizontal.normalized : (Vector3d)Vector3.ProjectOnPlane(vessel.transform.up, up).normalized;

            if (levelOffLeft > 0)
            {
                // Run over: hold level flight briefly, then hand back control.
                if (manual)
                {
                    Detach();
                    return;
                }
                FlyTo(s, 0, null, up, velocity, track, dt);
                levelOffLeft -= dt;
                if (levelOffLeft <= 0)
                    Detach();
                return;
            }

            // Terrain look-ahead about ten times a second (the missile covers ~100 m between refreshes).
            if (frame++ % LookaheadEveryFrames == 0)
            {
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = TerrainAsl(body, position + track * (i * PlutoConstants.LookaheadStepM));
                desiredAsl = PlutoTerrainLookahead.DesiredAltitudeAsl(samples, runHeight,
                    PlutoConstants.LookaheadStepM, PlutoConstants.LookaheadClimbDeg);
                groundAsl = samples[0];
            }
            double agl = vessel.altitude - groundAsl;
            bool inAtmosphere = vessel.altitude < body.atmosphereDepth;

            run.Update(dt, agl, desiredAsl, vessel.altitude, vessel.srfSpeed, manual,
                reactor != null && reactor.IsRunning, inAtmosphere, warheads != null ? warheads.warheadsLeft : 0);
            if (!run.Active)
            {
                Finish();
                return;
            }

            RunTarget target = FindTarget(body);
            double? heading = PlutoRunController.HeadingToTarget(target.Has, target.Has
                ? SignedAngle(track, Vector3d.Exclude(up, target.Position - position).normalized, up) : 0);
            FlyTo(s, run.TargetFlightPathDeg, heading, up, velocity, track, dt);
            s.mainThrottle = (float)throttlePid.Update(runMach - vessel.mach, dt);

            if (run.Phase == PlutoRunPhase.Follow && warheads != null)
                Release(body, target, track, position);
            if (!run.Active)
            {
                Finish();
                return;
            }
            status = run.Phase == PlutoRunPhase.Dive ? "Diving" : "Following terrain";
        }

        // Pitch toward the wanted flight-path angle; bank toward the target heading error (null = wings level).
        private void FlyTo(FlightCtrlState s, double flightPathDeg, double? headingErrorDeg, Vector3d up,
            Vector3d velocity, Vector3d track, float dt)
        {
            double speed = Math.Max(velocity.magnitude, 1);
            double currentFpa = Math.Asin(Math.Max(-1, Math.Min(1, Vector3d.Dot(velocity, up) / speed))) * 180 / Math.PI;
            double pitch = pitchPid.Update(flightPathDeg - currentFpa, dt);
            if (vessel.geeForce > PlutoConstants.MaxG)
                pitch *= PlutoConstants.MaxG / vessel.geeForce;

            double wantedBank = headingErrorDeg.HasValue ? PlutoRunController.WantedBankDeg(headingErrorDeg.Value) : 0;
            // The vessel's "top" is opposite its reference transform's forward (KSP convention).
            Vector3d forward = vessel.ReferenceTransform.up;
            Vector3d top = -vessel.ReferenceTransform.forward;
            Vector3d levelTop = Vector3d.Exclude(forward, up);
            // Pointing straight up there is no "level": hold the roll where it is.
            double bank = levelTop.sqrMagnitude < 1e-4
                ? 0
                : SignedAngle(levelTop.normalized, Vector3d.Exclude(forward, top).normalized, forward);
            // No pull while steeply banked or inverted: roll upright first.
            pitch *= PlutoRunController.PitchScaleForBank(bank);

            s.pitch = pitchSign * (float)pitch;
            s.roll = rollSign * (float)rollPid.Update(wantedBank - bank, dt);
            s.yaw = 0;
        }

        private void Release(CelestialBody body, RunTarget target, Vector3d track, Vector3d position)
        {
            if (warheads.releaseOnTarget && target.Has)
            {
                double ahead = Vector3d.Dot(target.Position - position, track);
                if (frame % PredictEveryFrames != 0 || ahead > PredictRangeM)
                    return;
                PlutoVec impact = warheads.PredictImpactBodyRelative();
                PlutoVec targetLocal = PlutoFallingObject.ToLocalPoint(body, target.Position);
                PlutoVec trackLocal = PlutoFallingObject.ToLocalVector(body, track);
                if (!toss.ShouldRelease(impact, targetLocal, trackLocal))
                    return;
                if (warheads.ReleaseWarhead(onlyIfSafe: true))
                    run.NotifyReleasedOnTarget();
                else
                {
                    // Releasing now would destroy the missile with its own warhead.
                    run.Abort("Release held: too slow or low, the missile would be inside the blast. Run aborted.");
                    Finish();
                }
                return;
            }
            if (!runOwnsAutoRelease && !warheads.autoRelease)
            {
                // No on-target release: drop a line of warheads on the auto-release timer.
                runOwnsAutoRelease = true;
                warheads.autoRelease = true;
            }
        }

        private struct RunTarget
        {
            public bool Has;
            public Vector3d Position;
        }

        private RunTarget FindTarget(CelestialBody body)
        {
            ITargetable t = vessel.targetObject;
            if (t != null && t.GetTransform() != null && !(t is CelestialBody))
                return new RunTarget { Has = true, Position = t.GetTransform().position };
            NavWaypoint nav = NavWaypoint.fetch;
            if (nav != null && nav.IsActive)
                return new RunTarget
                {
                    Has = true,
                    Position = body.GetWorldSurfacePosition(nav.Latitude, nav.Longitude, nav.Altitude),
                };
            return new RunTarget { Has = false };
        }

        private static double TerrainAsl(CelestialBody body, Vector3d world)
        {
            double terrain = body.TerrainAltitude(body.GetLatitude(world), body.GetLongitude(world), true);
            return body.ocean ? Math.Max(0, terrain) : terrain;
        }

        // Angle from a to b around axis, in degrees, positive counter-clockwise looking down the axis.
        private static double SignedAngle(Vector3d a, Vector3d b, Vector3d axis)
        {
            double angle = Vector3d.Angle(a, b);
            return Vector3d.Dot(axis, Vector3d.Cross(a, b)) < 0 ? -angle : angle;
        }

        private void Finish()
        {
            Post(run.Message);
            status = run.Phase == PlutoRunPhase.Done ? "Complete" : "Aborted";
            if (warheads != null && runOwnsAutoRelease)
                warheads.autoRelease = false;
            runOwnsAutoRelease = false;
            if (run.Phase == PlutoRunPhase.Done)
                levelOffLeft = LevelOffSeconds;
            else
                Detach();
            RefreshControls();
        }

        private void Detach()
        {
            if (!attached)
                return;
            attached = false;
            runActive = false;
            takeoffActive = false;
            takeoff = null;
            levelOffLeft = 0;
            if (subscribed != null)
            {
                subscribed.OnFlyByWire -= Fly;
                if (sasWasOn && subscribed == vessel)
                    vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                subscribed = null;
            }
            status = "Manual control";
            RefreshControls();
        }

        private void RefreshControls()
        {
            Events[nameof(CommenceRun)].active = !attached || takeoff != null;
            Events[nameof(AbortRun)].active = attached && takeoff == null;
        }

        private static void Post(string text)
        {
            if (!string.IsNullOrEmpty(text))
                ScreenMessages.PostScreenMessage(text, 4f, ScreenMessageStyle.UPPER_CENTER);
        }
    }
}
