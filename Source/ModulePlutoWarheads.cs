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
        [KSPField] public float autoReleaseSeconds = (float)PlutoConstants.AutoReleaseSeconds;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Warheads")]
        public int warheadsLeft = PlutoConstants.WarheadCount;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Auto-release"),
            UI_Toggle(disabledText = "Off", enabledText = "On", scene = UI_Scene.Flight)]
        public bool autoRelease;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Release on target (bombing run)"),
            UI_Toggle(disabledText = "Off", enabledText = "On", scene = UI_Scene.Flight)]
        public bool releaseOnTarget;

        [KSPField(isPersistant = true)] public double nextAutoReleaseUT = -1;

        private Transform hatch;
        private bool lastAutoRelease;

        public override string GetInfo() =>
            $"{PlutoConstants.WarheadCount} thermonuclear warheads, {PlutoConstants.WarheadMassT:0.#} t each " +
            "(historical sources give 14 to 26).\n" +
            $"Each is tossed from the top hatches and detonates on impact (1 Mt effect, {PlutoConstants.DamageRadiusM:0} m damage radius).";

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            hatch = part.FindModelTransform(hatchTransform);
            lastAutoRelease = autoRelease;
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
                ReleaseWarhead();
                nextAutoReleaseUT = now + autoReleaseSeconds;
            }
        }

        [KSPEvent(guiActive = true, guiName = "Release warhead")]
        public void ReleaseWarheadEvent() => ReleaseWarhead();

        [KSPAction("Release warhead")]
        public void ReleaseAction(KSPActionParam param) => ReleaseWarhead();

        [KSPAction("Toggle auto-release")]
        public void ToggleAutoReleaseAction(KSPActionParam param) => autoRelease = !autoRelease;

        // Throws one warhead out of the top hatch. Returns false if none are left or not in flight.
        public bool ReleaseWarhead()
        {
            if (!InFlight || warheadsLeft <= 0)
                return false;

            CelestialBody body = vessel.mainBody;
            Vector3 origin = hatch != null ? hatch.position : part.transform.position;
            Vector3 up = (origin - (Vector3)body.position).normalized;
            Vector3 velocity = (Vector3)vessel.srf_velocity + up * (float)PlutoConstants.TossUpMs;

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
            part.UpdateMass();
            GameEvents.onVesselWasModified.Fire(vessel);
            RefreshControls();
            ScreenMessages.PostScreenMessage(
                $"Warhead released ({warheadsLeft} left).", 3f, ScreenMessageStyle.UPPER_CENTER);
            return true;
        }

        // Where a warhead released now would land, in the body's own frame.
        public PlutoVec PredictImpactBodyRelative()
        {
            CelestialBody body = vessel.mainBody;
            Vector3 origin = hatch != null ? hatch.position : part.transform.position;
            Vector3 up = (origin - (Vector3)body.position).normalized;
            Vector3 velocity = (Vector3)vessel.srf_velocity + up * (float)PlutoConstants.TossUpMs;
            PlutoVec position = PlutoFallingObject.ToLocalPoint(body, origin);
            PlutoVec localVelocity = PlutoFallingObject.ToLocalVector(body, velocity);
            PlutoVec gravity = PlutoFallingObject.Gravity(body, position);
            return PlutoBallistics.PredictImpact(position, localVelocity, gravity, DragPerM(vessel.atmDensity),
                p => PlutoFallingObject.HeightAboveGround(body, p), 0.5, 60);
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
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) =>
            -(float)((PlutoConstants.WarheadCount - warheadsLeft) * PlutoConstants.WarheadMassT);

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.CONSTANTLY;
    }
}
