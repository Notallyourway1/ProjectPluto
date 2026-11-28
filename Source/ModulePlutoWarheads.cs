using System;
using UnityEngine;

namespace ProjectPluto
{
    // Sixteen 1 Mt warheads carried inside the missile and tossed out one at a time from the top hatches.
    public sealed class ModulePlutoWarheads : PartModule, IPartMassModifier
    {
        // Ballistic coefficient: drag acceleration = k·ρ/ρ₀·|v|·v for a 0.5 t bomb (gameplay estimate).
        private const double SeaLevelDragPerM = 7e-5;
        private const double SeaLevelDensity = 1.225;

        [KSPField] public string hatchTransform = "warheadHatch";
        // Gap between automatic releases (auto-release, or a bombing run with no target). Adjustable in flight.
        [KSPField(isPersistant = true, guiActive = true, guiName = "Release interval", guiUnits = " s",
            groupName = PlutoReadout.WarheadGroup, groupDisplayName = PlutoReadout.WarheadGroupTitle),
            UI_FloatRange(minValue = 2f, maxValue = 120f, stepIncrement = 1f, scene = UI_Scene.Flight)]
        public float autoReleaseSeconds = (float)PlutoConstants.AutoReleaseSeconds;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Warheads left",
            groupName = PlutoReadout.WarheadGroup, groupDisplayName = PlutoReadout.WarheadGroupTitle)]
        public int warheadsLeft = PlutoConstants.WarheadCount;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Auto-release", groupName = PlutoReadout.WarheadGroup, groupDisplayName = PlutoReadout.WarheadGroupTitle),
            UI_Toggle(disabledText = "Off", enabledText = "On", scene = UI_Scene.Flight)]
        public bool autoRelease;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Release on target (bombing run)",
            groupName = PlutoReadout.WarheadGroup, groupDisplayName = PlutoReadout.WarheadGroupTitle),
            UI_Toggle(disabledText = "Off", enabledText = "On", scene = UI_Scene.Flight)]
        public bool releaseOnTarget;

        [KSPField(isPersistant = true)] public double nextAutoReleaseUT = -1;

        // How often a held automatic release checks again whether it has become safe.
        private const double SafetyRetrySeconds = 1;

        private Transform hatch;
        private bool lastAutoRelease;
        private bool holdingWarned;

        public override string GetInfo() =>
            $"{PlutoConstants.WarheadCount} thermonuclear warheads, {PlutoConstants.WarheadMassT:0.#} t each " +
            "(historical sources give 14 to 26).\n" +
            $"Each is tossed from the top hatches and detonates on impact (1 Mt effect, {PlutoConstants.DamageRadiusM:0} m damage radius).";

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            hatch = part.FindModelTransform(hatchTransform);
            lastAutoRelease = autoRelease;
            // A shorter interval takes effect at once instead of after the old, longer wait.
            Fields[nameof(autoReleaseSeconds)].uiControlFlight.onFieldChanged = (field, old) =>
            {
                double now = Planetarium.GetUniversalTime();
                if (nextAutoReleaseUT > now + autoReleaseSeconds)
                    nextAutoReleaseUT = now + autoReleaseSeconds;
            };
            RefreshControls();
        }

        public bool InFlight => HighLogic.LoadedSceneIsFlight && vessel != null && !vessel.packed;

        public void FixedUpdate()
        {
            if (!InFlight)
                return;

            double now = Planetarium.GetUniversalTime();
            if (autoRelease && !lastAutoRelease)
                nextAutoReleaseUT = now;
            lastAutoRelease = autoRelease;

            if (autoRelease && warheadsLeft > 0 && nextAutoReleaseUT >= 0 && now >= nextAutoReleaseUT)
            {
                if (ReleaseWarhead(onlyIfSafe: true))
                {
                    nextAutoReleaseUT = now + autoReleaseSeconds;
                    holdingWarned = false;
                }
                else
                {
                    // Too slow or low: the missile would be caught in its own blast. Try again shortly.
                    nextAutoReleaseUT = now + SafetyRetrySeconds;
                    if (!holdingWarned)
                        ScreenMessages.PostScreenMessage(
                            "Auto-release holding: too slow or low — the missile would be inside the blast.",
                            5f, ScreenMessageStyle.UPPER_CENTER);
                    holdingWarned = true;
                }
            }
        }

        [KSPEvent(guiActive = true, guiName = "Release warhead", groupName = PlutoReadout.WarheadGroup, groupDisplayName = PlutoReadout.WarheadGroupTitle)]
        public void ReleaseWarheadEvent() => ReleaseWarhead();

        [KSPAction("Release warhead")]
        public void ReleaseAction(KSPActionParam param) => ReleaseWarhead();

        [KSPAction("Toggle auto-release")]
        public void ToggleAutoReleaseAction(KSPActionParam param) => autoRelease = !autoRelease;

        // Throws one warhead out of the top hatch. Returns false if none are left, not in flight, or
        // (onlyIfSafe) the missile would still be inside this warhead's blast.
        public bool ReleaseWarhead(bool onlyIfSafe = false)
        {
            if (!InFlight || warheadsLeft <= 0)
                return false;

            CelestialBody body = vessel.mainBody;
            ReleaseState(body, out Vector3 origin, out PlutoVec position, out PlutoVec localVelocity);
            bool unsafeRelease = InsideBlast(body, position, localVelocity);
            if (unsafeRelease && onlyIfSafe)
                return false;
            if (unsafeRelease)
                ScreenMessages.PostScreenMessage("Warning: too slow or low — the missile will be inside this warhead's blast.",
                    5f, ScreenMessageStyle.UPPER_CENTER);
            Vector3 velocity = body.transform.TransformDirection(
                new Vector3((float)localVelocity.X, (float)localVelocity.Y, (float)localVelocity.Z));

            GameObject bomb = CreateBombVisual(origin);
            PlutoFallingObject falling = PlutoFallingObject.Attach(bomb, body, origin, velocity);
            falling.DragPerM = DragPerM(vessel.atmDensity);
            var fuse = new PlutoWarheadFuse();
            falling.CustomUpdate = (obj, height) =>
            {
                if (!fuse.Update(TimeWarp.fixedDeltaTime, height))
                    return false;
                PlutoBlast.Detonate(obj.WorldPosition, obj.Body, (float)PlutoConstants.DamageRadiusM);
                return true;
            };

            warheadsLeft--;
            PlutoAudio.Play(part, PlutoAudio.Cue.WarheadRelease);
            part.UpdateMass();
            GameEvents.onVesselWasModified.Fire(vessel);
            RefreshControls();
            ScreenMessages.PostScreenMessage(
                $"Warhead released ({warheadsLeft} left).", 3f, ScreenMessageStyle.UPPER_CENTER);
            return true;
        }

        // Where a warhead released now would land, in the body's own frame.
        public PlutoVec PredictImpactBodyRelative() => PredictImpactBodyRelative(out _);

        private PlutoVec PredictImpactBodyRelative(out double fallTime)
        {
            CelestialBody body = vessel.mainBody;
            ReleaseState(body, out _, out PlutoVec position, out PlutoVec localVelocity);
            return Predict(body, position, localVelocity, out fallTime);
        }

        private PlutoVec Predict(CelestialBody body, PlutoVec position, PlutoVec localVelocity, out double fallTime) =>
            PlutoBallistics.PredictImpact(position, localVelocity, PlutoFallingObject.Gravity(body, position),
                DragPerM(vessel.atmDensity), p => PlutoFallingObject.HeightAboveGround(body, p), out fallTime, 0.5, 60);

        // Hatch position and the warhead's toss velocity (up and backward), in the body's frame.
        private void ReleaseState(CelestialBody body, out Vector3 origin, out PlutoVec position, out PlutoVec velocity)
        {
            origin = hatch != null ? hatch.position : part.transform.position;
            position = PlutoFallingObject.ToLocalPoint(body, origin);
            PlutoVec up = position * (1 / System.Math.Max(position.Length, 1));
            velocity = PlutoBallistics.TossVelocity(PlutoFallingObject.ToLocalVector(body, vessel.srf_velocity), up);
        }

        private bool InsideBlast(CelestialBody body, PlutoVec position, PlutoVec tossVelocity)
        {
            PlutoVec impact = Predict(body, position, tossVelocity, out double fallTime);
            PlutoVec missileVelocity = PlutoFallingObject.ToLocalVector(body, vessel.srf_velocity);
            return PlutoBallistics.MissileInsideBlast(position, missileVelocity, impact, fallTime, PlutoConstants.DamageRadiusM);
        }

        private static double DragPerM(double density) => SeaLevelDragPerM * Math.Max(0, density) / SeaLevelDensity;

        private GameObject CreateBombVisual(Vector3 position)
        {
            GameObject bomb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bomb.name = "Pluto warhead";
            Collider c = bomb.GetComponent<Collider>();
            if (c != null)
                Destroy(c);
            bomb.transform.position = position;
            bomb.transform.rotation = part.transform.rotation;
            bomb.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            Renderer skin = part.FindModelComponent<Renderer>();
            Renderer renderer = bomb.GetComponent<Renderer>();
            if (skin != null && renderer != null)
                renderer.sharedMaterial = skin.sharedMaterial;
            return bomb;
        }

        private void RefreshControls()
        {
            Events[nameof(ReleaseWarheadEvent)].active = warheadsLeft > 0;
            Fields[nameof(autoRelease)].guiActive = warheadsLeft > 0;
            Fields[nameof(autoReleaseSeconds)].guiActive = warheadsLeft > 0;
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) =>
            -(float)((PlutoConstants.WarheadCount - warheadsLeft) * PlutoConstants.WarheadMassT);

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.CONSTANTLY;
    }
}
