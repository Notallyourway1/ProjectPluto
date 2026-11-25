using UnityEngine;

namespace ProjectPluto
{
    // The Tory reactor: warms up during the boost, powers the ramjet, irradiates kerbals nearby,
    // SCRAMs when it loses its cooling air, and breaches if the missile is destroyed while hot.
    public sealed class ModulePlutoReactor : PartModule
    {
        private const string SaveKey = "reactorSave";

        [KSPField] public string ramjetEngineID = "Ramjet";
        [KSPField] public float electricRate = 5f;

        [KSPField(guiActive = true, guiName = "Reactor")]
        public string stateText = "Cold";
        [KSPField(guiActive = true, guiName = "Reactor power", guiFormat = "F0", guiUnits = " MW")]
        public float powerMW;
        [KSPField(guiActive = true, guiName = "Core temperature", guiFormat = "F0", guiUnits = " °C")]
        public float coreTempC;
        [KSPField(guiActive = true, guiName = "Cooling airflow", guiFormat = "P0")]
        public float airflow;
        [KSPField(guiActive = true, guiName = "Exhaust temperature", guiFormat = "F0", guiUnits = " °C")]
        public float exhaustTempC;
        [KSPField(guiActive = true, guiName = "Inlet")]
        public string inletText = "Started";

        private const string NozzleGlowTransform = "NozzleGlow";
        // Seconds for the nozzle to heat up or cool down visibly.
        private const float GlowTimeConstant = 3f;

        private ModuleEnginesFX ramjet;
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private static readonly int EmissiveTextureId = Shader.PropertyToID("_Emissive");
        private const double IdealGasConstant = 8.314462618; // J/(mol·K)

        private Material nozzleGlow;
        private Color lastGlow = Color.clear;
        private float ramjetIsp;
        private PlutoExhaustTrail trail;
        private readonly PlutoInlet inlet = new PlutoInlet();
        private float glowTempC = (float)PlutoConstants.AmbientC;
        private ModulePlutoBoosters boosters;
        private bool breachHandled;

        public PlutoReactorModel Model { get; private set; } = new PlutoReactorModel();
        public bool IsRunning => Model.State == PlutoReactorState.Running;

        public override string GetInfo() =>
            $"Unshielded nuclear reactor, {PlutoConstants.ReactorMaxMW:0} MW (Tory design power).\n" +
            $"Warms up in {PlutoConstants.WarmupSeconds:0} s from booster ignition.\n" +
            "Heats rammed air for thrust (ideal ramjet: no fuel, no thrust at rest,\n" +
            "stops near Mach 4.8 at sea level, where ram heating reaches core temperature).\n" +
            "Needs that air for cooling; SCRAMs if it overheats.\n" +
            "Doses kerbals nearby with radiation.";

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasValue(SaveKey))
                Model = PlutoReactorModel.Load(node.GetValue(SaveKey));
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.SetValue(SaveKey, Model.Save(), true);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            boosters = part.FindModuleImplementing<ModulePlutoBoosters>();
            foreach (ModuleEnginesFX engine in part.FindModulesImplementing<ModuleEnginesFX>())
            {
                if (engine.engineID == ramjetEngineID)
                    ramjet = engine;
            }
            Transform nozzle = part.FindModelTransform(NozzleGlowTransform);
            Renderer nozzleRenderer = nozzle != null ? nozzle.GetComponent<Renderer>() : null;
            if (nozzleRenderer != null)
            {
                nozzleGlow = nozzleRenderer.material;
                // Make sure the emissive map does not mask the glow out.
                if (nozzleGlow.HasProperty(EmissiveTextureId))
                    nozzleGlow.SetTexture(EmissiveTextureId, Texture2D.whiteTexture);
                nozzleGlow.SetColor(EmissiveColorId, Color.black);
            }
            if (ramjet != null)
                ramjetIsp = ramjet.atmosphereCurve.Evaluate(0f);
            if (HighLogic.LoadedSceneIsFlight)
            {
                part.OnJustAboutToBeDestroyed += OnAboutToBeDestroyed;
                ApplyThrust(0);
                RefreshControls();
            }
        }

        public void OnDestroy()
        {
            trail?.Release();
            if (nozzleGlow != null)
                Destroy(nozzleGlow);
            if (part != null)
                part.OnJustAboutToBeDestroyed -= OnAboutToBeDestroyed;
        }

        // Called by the booster module at burnout: the ramjet takes over.
        public void OnBoostersGone()
        {
            Model.Start();
            if (ramjet != null && !ramjet.EngineIgnited)
                ramjet.Activate();
            RefreshControls();
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null || vessel.packed)
                return;

            if (Model.State == PlutoReactorState.Cold && boosters != null && boosters.BoostEngineIgnited)
                Model.Start();

            double dt = TimeWarp.fixedDeltaTime;
            // Inlet overspeed: an unstarted scoop captures only a fraction of its air.
            switch (inlet.Update(dt, vessel.mach))
            {
                case PlutoInlet.Change.Unstarted:
                    ScreenMessages.PostScreenMessage(
                        $"Inlet unstart above Mach {PlutoConstants.UnstartMach:0.0}: thrust and cooling lost. " +
                        $"Slow below Mach {PlutoConstants.RestartMach:0.0} to restart.", 6f, ScreenMessageStyle.UPPER_CENTER);
                    break;
                case PlutoInlet.Change.Restarted:
                    ScreenMessages.PostScreenMessage("Inlet restarted.", 3f, ScreenMessageStyle.UPPER_CENTER);
                    break;
            }
            inletText = inlet.Started ? "Started" : "UNSTARTED";
            double area = PlutoConstants.IntakeAreaM2 * inlet.CaptureFactor;

            // How much heat the rammed air can carry away decides cooling; the same physics gives thrust.
            CelestialBody body = vessel.mainBody;
            double gamma = body.atmosphereAdiabaticIndex > 1 ? body.atmosphereAdiabaticIndex : PlutoRamjet.Gamma;
            double gasConstant = body.atmosphereMolarMass > 0
                ? IdealGasConstant / body.atmosphereMolarMass
                : PlutoRamjet.GasConstant;
            PlutoRamjet.Result air = PlutoRamjet.Compute(Model.PowerMW * 1e6, vessel.atmDensity, vessel.srfSpeed,
                vessel.atmosphericTemperature, area, gamma, gasConstant);
            double flow = air.CoolingFraction;
            double demand = Model.State == PlutoReactorState.Warming
                ? 1
                : PlutoReactorModel.GovernedDemand(vessel.ctrlState.mainThrottle, flow);
            PlutoReactorState before = Model.State;
            Model.Update(dt, demand, flow);

            if (before == PlutoReactorState.Running && Model.State == PlutoReactorState.Scrammed)
                ScreenMessages.PostScreenMessage("Reactor SCRAM: core overheated (not enough cooling air).",
                    5f, ScreenMessageStyle.UPPER_CENTER);
            if (before != Model.State)
                RefreshControls();

            air = PlutoRamjet.Compute(Model.PowerMW * 1e6, vessel.atmDensity, vessel.srfSpeed,
                vessel.atmosphericTemperature, area, gamma, gasConstant);
            ApplyThrust(air.ThrustN);
            RecordTrail(air.ThrustN);
            double heatKW = PlutoReactorModel.StructureHeatKW(Model.PowerMW, Model.CoreTempC);
            if (heatKW > 0 && Model.State != PlutoReactorState.Breached)
                part.AddThermalFlux(heatKW);
            exhaustTempC = (float)(air.ExitTempK - 273.15);
            if (Model.IsPowered)
            {
                part.RequestResource("ElectricCharge", -electricRate * dt);
                PlutoRadiationScenario.Instance?.IrradiateFrom(vessel.mainBody, part.transform.position,
                    Model.PowerMW, dt, vessel);
            }

            powerMW = (float)Model.PowerMW;
            coreTempC = (float)Model.CoreTempC;
            airflow = (float)flow;
            stateText = Model.State.ToString();
        }

        // The nozzle glows with the exhaust temperature while the ramjet runs, and cools when it stops.
        public void Update()
        {
            if (nozzleGlow == null || !HighLogic.LoadedSceneIsFlight)
                return;
            bool running = ramjet != null && ramjet.EngineIgnited && ramjet.finalThrust > 0.1f;
            float target = running ? exhaustTempC : (float)PlutoConstants.AmbientC;
            glowTempC += (target - glowTempC) * (1f - Mathf.Exp(-Time.deltaTime / GlowTimeConstant));
            PlutoGlow.ColorForTemp(glowTempC, out double r, out double g, out double b);
            var colour = new Color((float)r, (float)g, (float)b, 1f);
            // Skip the material update when the change cannot be seen.
            if (Mathf.Abs(colour.r - lastGlow.r) + Mathf.Abs(colour.g - lastGlow.g) < 0.004f)
                return;
            lastGlow = colour;
            nozzleGlow.SetColor(EmissiveColorId, colour);
        }

        // While the ramjet is pushing, fission fragments in the exhaust leave a radioactive trail.
        private void RecordTrail(double thrustN)
        {
            if (!Model.IsPowered || thrustN <= 0 || ramjet == null || ramjet.thrustTransforms.Count == 0)
                return;
            double now = Planetarium.GetUniversalTime();
            if (trail == null || trail.Body != vessel.mainBody || now - trail.LastRecord > PlutoExhaustTrail.GapSeconds)
            {
                trail?.Release();
                trail = PlutoExhaustTrail.Create(vessel.mainBody, vessel);
            }
            trail.Record(ramjet.thrustTransforms[0].position, Model.PowerMW, now);
        }

        // The ramjet's thrust comes from the physics model; KSP's engine only delivers it.
        private void ApplyThrust(double thrustN)
        {
            if (ramjet == null)
                return;
            // The throttle KSP's engine actually applies (no spool lag: useEngineResponseTime is off).
            double throttle = ramjet.EngineIgnited ? ramjet.currentThrottle : 0;
            float rating = (float)PlutoRamjet.EngineRatingKN(thrustN, throttle);
            ramjet.thrustPercentage = 100f;
            ramjet.maxThrust = rating;
            ramjet.maxFuelFlow = ramjetIsp > 0 ? rating / (ramjetIsp * ramjet.g) : 0f;
        }

        [KSPEvent(guiActive = true, guiName = "SCRAM reactor")]
        public void Scram()
        {
            Model.Scram();
            RefreshControls();
        }

        [KSPEvent(guiActive = true, guiName = "Restart reactor")]
        public void Restart()
        {
            if (Model.CoreTempC >= PlutoConstants.RestartTempC)
            {
                ScreenMessages.PostScreenMessage(
                    $"Core too hot to restart ({Model.CoreTempC:0} °C; below {PlutoConstants.RestartTempC:0} °C needed).",
                    4f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            Model.Restart();
            RefreshControls();
        }

        [KSPAction("SCRAM reactor")]
        public void ScramAction(KSPActionParam param) => Scram();

        [KSPAction("Restart reactor")]
        public void RestartAction(KSPActionParam param) => Restart();

        private void RefreshControls()
        {
            Events[nameof(Scram)].active = Model.IsPowered;
            Events[nameof(Restart)].active = Model.State == PlutoReactorState.Scrammed;
        }

        // A hot reactor that is destroyed cracks open and leaves a radiation source behind.
        public void TriggerBreach()
        {
            if (breachHandled || vessel == null)
                return;
            breachHandled = true;
            if (Model.PowerFraction <= PlutoConstants.BreachPowerThreshold)
                return;

            Vector3 position = part.transform.position;
            CelestialBody body = vessel.mainBody;
            Model.Breach();
            FXMonger.Explode(part, position, 0.6f);
            PlutoRadiationScenario.Instance?.AddBreach(body, position,
                Planetarium.GetUniversalTime() + PlutoConstants.BreachSeconds);
            ScreenMessages.PostScreenMessage("Pluto reactor breached: radiation hazard at the crash site.",
                5f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void OnAboutToBeDestroyed()
        {
            if (HighLogic.LoadedSceneIsFlight && vessel != null && !vessel.packed)
                TriggerBreach();
        }
    }
}
