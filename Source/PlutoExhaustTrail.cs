using System.Collections.Generic;
using UnityEngine;

namespace ProjectPluto
{
    // The radioactive exhaust trail left by a running Pluto ramjet. It is its own object so it lingers
    // after the missile has gone; it doses crew under it and draws as a faint, fading haze line.
    // The line lives in the planet's own frame, so it only needs rebuilding when points change.
    public sealed class PlutoExhaustTrail : MonoBehaviour
    {
        // Beyond this a trail point's dose is negligible; skip it.
        private const float DoseRangeM = 3000f;
        // Dose is integrated at 10 Hz; the total does not need physics-rate updates.
        private const double DoseIntervalSeconds = 0.1;
        // A pause longer than this (SCRAM, shutdown) starts a new trail instead of joining the gap.
        public const double GapSeconds = 3;
        private const float WidthM = 8f;
        private static readonly Color Haze = new Color(0.72f, 0.76f, 0.66f, 1f);
        private static readonly string[] ShaderNames =
        {
            "Legacy Shaders/Particles/Alpha Blended",
            "KSP/Particles/Alpha Blended",
            "Sprites/Default"
        };

        private readonly PlutoTrailModel model = new PlutoTrailModel();
        private readonly List<Part> crewedParts = new List<Part>();
        private readonly Vector3[] linePoints = new Vector3[PlutoTrailModel.MaxPoints + 1];
        private readonly Vector3[] worldPoints = new Vector3[PlutoTrailModel.MaxPoints];
        private readonly double[] strengths = new double[PlutoTrailModel.MaxPoints];
        private CelestialBody body;
        private Vessel source;
        private LineRenderer line;
        private Material material;
        private bool ownerAlive = true;
        private bool hasLive;
        private Vector3 liveLocal;
        private int drawnCount = -1;
        private double lastRecord = double.NegativeInfinity;
        private double doseTimer;

        public CelestialBody Body => body;
        public double LastRecord => lastRecord;

        public static PlutoExhaustTrail Create(CelestialBody body, Vessel source)
        {
            var go = new GameObject("Pluto exhaust trail");
            // Parented to the planet: its points stay put through floating-origin shifts and rotation.
            go.transform.SetParent(body.transform, false);
            var trail = go.AddComponent<PlutoExhaustTrail>();
            trail.body = body;
            trail.source = source;
            trail.line = go.AddComponent<LineRenderer>();
            trail.line.useWorldSpace = false;
            trail.line.widthMultiplier = WidthM;
            trail.line.numCapVertices = 2;
            trail.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.line.receiveShadows = false;
            Shader shader = null;
            foreach (string name in ShaderNames)
            {
                shader = Shader.Find(name);
                if (shader != null && shader.isSupported)
                    break;
            }
            if (shader != null)
            {
                trail.material = new Material(shader) { mainTexture = Texture2D.whiteTexture };
                trail.line.material = trail.material;
            }
            else
                trail.line.enabled = false;
            return trail;
        }

        // Drops a trail point when due, and keeps the line's head at the nozzle.
        public void Record(Vector3 worldPosition, double powerMW, double now)
        {
            model.AddIfDue(PlutoFallingObject.ToLocalPoint(body, worldPosition), powerMW, now);
            liveLocal = body.transform.InverseTransformPoint(worldPosition);
            hasLive = true;
            lastRecord = now;
            drawnCount = -1;
        }

        // The missile stopped feeding this trail (destroyed, changed body or paused too long).
        public void Release()
        {
            ownerAlive = false;
            hasLive = false;
            drawnCount = -1;
        }

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }

        private void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || body == null)
            {
                Destroy(gameObject);
                return;
            }
            double now = Planetarium.GetUniversalTime();
            int before = model.Count;
            model.Prune(now);
            if (model.Count != before)
                drawnCount = -1;
            if (model.Count == 0)
            {
                if (!ownerAlive)
                    Destroy(gameObject);
                return;
            }

            doseTimer += TimeWarp.fixedDeltaTime;
            if (doseTimer >= DoseIntervalSeconds)
            {
                Irradiate(now, doseTimer);
                doseTimer = 0;
            }
        }

        private void Irradiate(double now, double dt)
        {
            PlutoRadiationScenario scenario = PlutoRadiationScenario.Instance;
            if (scenario == null)
                return;

            int count = model.Count;
            // Bounding sphere of the trail, to skip whole vessels that are far from all of it.
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int i = 0; i < count; i++)
            {
                worldPoints[i] = PlutoFallingObject.ToWorldPoint(body, model[i].Position);
                strengths[i] = model.StrengthMW(i, now);
                min = Vector3.Min(min, worldPoints[i]);
                max = Vector3.Max(max, worldPoints[i]);
            }
            Vector3 centre = (min + max) * 0.5f;
            float reach = (max - min).magnitude * 0.5f + DoseRangeM;

            crewedParts.Clear();
            foreach (Vessel v in FlightGlobals.VesselsLoaded)
            {
                if (v == null || v == source || v.mainBody != body)
                    continue;
                if ((v.transform.position - centre).sqrMagnitude > reach * reach)
                    continue;
                foreach (Part p in v.parts)
                {
                    if (p.protoModuleCrew != null && p.protoModuleCrew.Count > 0)
                        crewedParts.Add(p);
                }
            }

            foreach (Part p in crewedParts)
            {
                if (p == null || p.vessel == null)
                    continue;
                Vector3 where = p.transform.position;
                double sv = 0;
                for (int i = 0; i < count; i++)
                {
                    float distance = (worldPoints[i] - where).magnitude;
                    if (distance < DoseRangeM)
                        sv += PlutoRadiation.DoseRate(strengths[i], distance) * dt;
                }
                if (sv > 0)
                    scenario.DosePart(p.vessel, p, sv);
            }
            scenario.ApplyDeaths();
        }

        private void LateUpdate()
        {
            if (line == null || !line.enabled)
                return;
            int count = model.Count;
            int total = count + (hasLive && count > 0 ? 1 : 0);
            if (total != drawnCount)
            {
                for (int i = 0; i < count; i++)
                {
                    PlutoVec p = model[i].Position;
                    linePoints[i] = new Vector3((float)p.X, (float)p.Y, (float)p.Z);
                }
                if (total > count)
                    linePoints[count] = liveLocal;
                line.positionCount = total;
                line.SetPositions(linePoints);
                drawnCount = total;
            }
            else if (hasLive && count > 0)
            {
                line.SetPosition(count, liveLocal);
            }
            if (count == 0)
                return;

            // Oldest end faint, newest end strongest.
            double now = Planetarium.GetUniversalTime();
            float newest = (float)model.Fade(count - 1, now);
            float oldest = (float)model.Fade(0, now);
            line.startColor = new Color(Haze.r, Haze.g, Haze.b, 0.25f * oldest);
            line.endColor = new Color(Haze.r, Haze.g, Haze.b, 0.25f * newest);
        }
    }
}
