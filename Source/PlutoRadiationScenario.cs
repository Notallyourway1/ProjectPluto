using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ProjectPluto
{
    // Keeps each kerbal's accumulated radiation dose and the radiation left at reactor crash sites,
    // and saves both with the game.
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.FLIGHT, GameScenes.SPACECENTER,
        GameScenes.TRACKSTATION)]
    public sealed class PlutoRadiationScenario : ScenarioModule
    {
        private sealed class Breach
        {
            public int BodyIndex;
            public double Latitude, Longitude, Altitude, ExpiresUT;
        }

        public static PlutoRadiationScenario Instance { get; private set; }

        private const double BreachIntervalSeconds = 0.1;
        private double breachTimer;

        private readonly Dictionary<string, double> doses = new Dictionary<string, double>();
        private readonly List<Breach> breaches = new List<Breach>();
        // Reused every physics frame so dosing allocates nothing.
        private readonly List<KeyValuePair<Vessel, ProtoCrewMember>> dying = new List<KeyValuePair<Vessel, ProtoCrewMember>>();

        public override void OnAwake()
        {
            base.OnAwake();
            Instance = this;
        }

        public void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public double DoseOf(string kerbalName) => doses.TryGetValue(kerbalName, out double d) ? d : 0;

        public void AddBreach(CelestialBody body, Vector3 worldPosition, double expiresUT)
        {
            breaches.Add(new Breach
            {
                BodyIndex = body.flightGlobalsIndex,
                Latitude = body.GetLatitude(worldPosition),
                Longitude = body.GetLongitude(worldPosition),
                Altitude = body.GetAltitude(worldPosition),
                ExpiresUT = expiresUT,
            });
        }

        // Doses every loaded kerbal on the same body from a source at worldPosition.
        public void IrradiateFrom(CelestialBody body, Vector3 worldPosition, double powerMW, double dt, Vessel source)
        {
            if (powerMW <= 0 || dt <= 0)
                return;
            dying.Clear();
            foreach (Vessel v in FlightGlobals.VesselsLoaded)
            {
                if (v == null || v == source || v.mainBody != body)
                    continue;
                foreach (Part p in v.parts)
                {
                    if (p.protoModuleCrew == null || p.protoModuleCrew.Count == 0)
                        continue;
                    double distance = (p.transform.position - worldPosition).magnitude;
                    DosePart(v, p, PlutoRadiation.DoseRate(powerMW, distance) * dt);
                }
            }
            ApplyDeaths();
        }

        // Adds a dose to everyone aboard a part; deaths wait for ApplyDeaths so crew lists stay valid.
        public void DosePart(Vessel v, Part p, double sv)
        {
            foreach (ProtoCrewMember kerbal in p.protoModuleCrew)
            {
                if (AddDose(kerbal, sv))
                    dying.Add(new KeyValuePair<Vessel, ProtoCrewMember>(v, kerbal));
            }
        }

        public void ApplyDeaths()
        {
            for (int i = 0; i < dying.Count; i++)
                Kill(dying[i].Key, dying[i].Value);
            dying.Clear();
        }

        // Returns true when this dose takes the kerbal past the lethal threshold.
        private bool AddDose(ProtoCrewMember kerbal, double sv)
        {
            if (sv <= 0 || kerbal.rosterStatus == ProtoCrewMember.RosterStatus.Dead)
                return false;
            double before = DoseOf(kerbal.name);
            double after = before + sv;
            doses[kerbal.name] = after;
            switch (PlutoRadiation.Crossed(before, after))
            {
                case PlutoRadiation.Threshold.Warn1:
                    Message($"{kerbal.name} has absorbed {after:0.00} Sv of radiation.");
                    break;
                case PlutoRadiation.Threshold.Warn2:
                    Message($"{kerbal.name} has radiation sickness ({after:0.0} Sv).");
                    break;
                case PlutoRadiation.Threshold.Lethal:
                    Message($"{kerbal.name} received a lethal radiation dose ({after:0.0} Sv).");
                    return true;
            }
            return false;
        }

        private static void Kill(Vessel v, ProtoCrewMember kerbal)
        {
            if (v.isEVA)
            {
                v.rootPart?.explode();
                return;
            }
            // Leave IVA first if the player is looking through this kerbal's eyes.
            if (CameraManager.Instance != null
                && CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA
                && v == FlightGlobals.ActiveVessel)
                CameraManager.Instance.SetCameraFlight();
            Part seat = kerbal.seat != null ? kerbal.seat.part : null;
            if (seat == null)
            {
                foreach (Part p in v.parts)
                {
                    if (p.protoModuleCrew.Contains(kerbal))
                        seat = p;
                }
            }
            if (seat != null)
                seat.RemoveCrewmember(kerbal);
            kerbal.Die();
            GameEvents.onCrewKilled.Fire(new EventReport(FlightEvents.CREW_KILLED, seat,
                kerbal.name, "radiation", 0, "Lethal radiation dose from a Pluto reactor", 0f));
            v.CrewListSetDirty();
            GameEvents.onVesselCrewWasModified.Fire(v);
        }

        private static void Message(string text) =>
            ScreenMessages.PostScreenMessage(text, 5f, ScreenMessageStyle.UPPER_LEFT);

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || breaches.Count == 0)
                return;
            // Dose is integrated at 10 Hz: the total does not need physics-rate updates.
            breachTimer += TimeWarp.fixedDeltaTime;
            if (breachTimer < BreachIntervalSeconds)
                return;
            double dt = breachTimer;
            breachTimer = 0;
            double now = Planetarium.GetUniversalTime();
            for (int i = breaches.Count - 1; i >= 0; i--)
            {
                Breach b = breaches[i];
                if (b.ExpiresUT <= now)
                {
                    breaches.RemoveAt(i);
                    continue;
                }
                if (b.BodyIndex < 0 || b.BodyIndex >= FlightGlobals.Bodies.Count)
                    continue;
                CelestialBody body = FlightGlobals.Bodies[b.BodyIndex];
                Vector3 position = body.GetWorldSurfacePosition(b.Latitude, b.Longitude, b.Altitude);
                IrradiateFrom(body, position, PlutoConstants.ReactorMaxMW * PlutoConstants.BreachStrength, dt, null);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            foreach (var entry in doses)
            {
                ConfigNode k = node.AddNode("KERBAL");
                k.AddValue("name", entry.Key);
                k.AddValue("dose", entry.Value.ToString("R", CultureInfo.InvariantCulture));
            }
            foreach (Breach b in breaches)
            {
                ConfigNode n = node.AddNode("BREACH");
                n.AddValue("body", b.BodyIndex);
                n.AddValue("lat", b.Latitude.ToString("R", CultureInfo.InvariantCulture));
                n.AddValue("lon", b.Longitude.ToString("R", CultureInfo.InvariantCulture));
                n.AddValue("alt", b.Altitude.ToString("R", CultureInfo.InvariantCulture));
                n.AddValue("expires", b.ExpiresUT.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            doses.Clear();
            breaches.Clear();
            foreach (ConfigNode k in node.GetNodes("KERBAL"))
            {
                string name = k.GetValue("name");
                if (!string.IsNullOrEmpty(name) && Parse(k, "dose", out double d))
                    doses[name] = d;
            }
            foreach (ConfigNode n in node.GetNodes("BREACH"))
            {
                var b = new Breach();
                if (int.TryParse(n.GetValue("body"), out b.BodyIndex)
                    && Parse(n, "lat", out b.Latitude) && Parse(n, "lon", out b.Longitude)
                    && Parse(n, "alt", out b.Altitude) && Parse(n, "expires", out b.ExpiresUT))
                    breaches.Add(b);
            }
        }

        private static bool Parse(ConfigNode n, string key, out double value) =>
            double.TryParse(n.GetValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
