using System.Collections.Generic;
using UnityEngine;

namespace ProjectPluto
{
    // The three strap-on boosters are part of the missile. When their solid fuel runs out they are
    // hidden, thrown off as visual debris, and their mass and drag are removed.
    public sealed class ModulePlutoBoosters : PartModule, IPartMassModifier, IMultipleDragCube
    {
        private const string BoostedCube = "Boosted";
        private const string CleanCube = "Clean";

        [KSPField] public string boostEngineID = "Boost";
        [KSPField] public string boosterTransforms = "Booster1,Booster2,Booster3";
        [KSPField] public float boosterDryMass = 3f;
        [KSPField] public float debrisPush = 8f;
        [KSPField] public float debrisLifetime = 60f;

        [KSPField(isPersistant = true)] public bool boostersJettisoned;

        private ModuleEnginesFX boostEngine;
        private readonly List<Transform> boosters = new List<Transform>();

        public bool BoostEngineIgnited => boostEngine != null && boostEngine.EngineIgnited && !boostersJettisoned;

        public override string GetInfo() =>
            $"Three integrated solid boosters ({boosterDryMass:0.#} t empty).\n" +
            "They detach by themselves when their fuel runs out.";

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            FindParts();
            if (boostersJettisoned)
                ApplyJettisonedState();
            else
                SetDragCube(BoostedCube);
        }

        private void SetDragCube(string active)
        {
            if (part.DragCubes == null || part.DragCubes.Cubes.Count < 2)
                return;
            part.DragCubes.SetCubeWeight(BoostedCube, active == BoostedCube ? 1f : 0f);
            part.DragCubes.SetCubeWeight(CleanCube, active == CleanCube ? 1f : 0f);
            part.DragCubes.ForceUpdate(true, true);
        }

        private void FindParts()
        {
            boosters.Clear();
            foreach (string name in boosterTransforms.Split(','))
            {
                Transform t = part.FindModelTransform(name.Trim());
                if (t != null)
                    boosters.Add(t);
            }
            boostEngine = null;
            foreach (ModuleEnginesFX engine in part.FindModulesImplementing<ModuleEnginesFX>())
            {
                if (engine.engineID == boostEngineID)
                    boostEngine = engine;
            }
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || boostersJettisoned || boostEngine == null || vessel == null
                || vessel.packed)
                return;

            if (boostEngine.EngineIgnited && (boostEngine.flameout || SolidFuelLeft() <= 0.01))
                Jettison(true);
        }

        private double SolidFuelLeft()
        {
            PartResource fuel = part.Resources["SolidFuel"];
            return fuel != null ? fuel.amount : 0;
        }

        [KSPEvent(guiActive = true, guiName = "Jettison boosters")]
        public void JettisonBoosters()
        {
            if (HighLogic.LoadedSceneIsFlight && !boostersJettisoned)
                Jettison(true);
        }

        [KSPAction("Jettison boosters")]
        public void JettisonAction(KSPActionParam param) => JettisonBoosters();

        private void Jettison(bool spawnDebris)
        {
            boostersJettisoned = true;
            if (spawnDebris && vessel != null)
                SpawnDebris();

            if (boostEngine != null)
            {
                boostEngine.Shutdown();
                boostEngine.allowRestart = false;
            }
            PartResource fuel = part.Resources["SolidFuel"];
            if (fuel != null)
                fuel.amount = 0;

            ApplyJettisonedState();
            GameEvents.onVesselWasModified.Fire(vessel);
            part.FindModuleImplementing<ModulePlutoReactor>()?.OnBoostersGone();
        }

        private void ApplyJettisonedState()
        {
            foreach (Transform t in boosters)
                t.gameObject.SetActive(false);
            if (boostEngine != null)
            {
                boostEngine.Events["Activate"].active = false;
                boostEngine.Events["Shutdown"].active = false;
                boostEngine.stagingEnabled = false;
            }
            Events[nameof(JettisonBoosters)].active = false;
            SetDragCube(CleanCube);
            part.UpdateMass();
        }

        private void SpawnDebris()
        {
            CelestialBody body = vessel.mainBody;
            Vector3 surfaceVelocity = vessel.srf_velocity;
            foreach (Transform booster in boosters)
            {
                GameObject copy = Instantiate(booster.gameObject, booster.position, booster.rotation);
                foreach (Collider c in copy.GetComponentsInChildren<Collider>(true))
                    Destroy(c);
                copy.SetActive(true);
                Vector3 outward = Vector3.ProjectOnPlane(booster.position - part.transform.position, part.transform.up);
                Vector3 push = outward.normalized * debrisPush - part.transform.up * (debrisPush * 0.5f);
                PlutoFallingObject falling = PlutoFallingObject.Attach(copy, body, booster.position, surfaceVelocity + push);
                falling.DragPerM = 2e-4;
                falling.MaxLifetime = debrisLifetime;
            }
        }

        // Mass: the part config includes the empty boosters; remove them once gone.
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) =>
            boostersJettisoned ? -boosterDryMass : 0f;

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.CONSTANTLY;

        // Two drag cubes: with and without the boosters.
        public string[] GetDragCubeNames() => new[] { BoostedCube, CleanCube };

        public void AssumeDragCubePosition(string name)
        {
            if (boosters.Count == 0)
                FindParts();
            bool show = name == BoostedCube;
            foreach (Transform t in boosters)
                t.gameObject.SetActive(show);
        }

        public bool UsesProceduralDragCubes() => false;

        public bool IsMultipleCubesActive => true;
    }
}
