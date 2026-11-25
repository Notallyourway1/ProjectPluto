using System.Collections.Generic;
using UnityEngine;

namespace ProjectPluto
{
    // The radioactive exhaust trail left by a running Pluto ramjet. It is its own object so it lingers
    // after the missile has gone; it doses crew under it and draws as a faint, fading haze line.
    public sealed class PlutoExhaustTrail : MonoBehaviour
    {
        // Beyond this a trail point's dose is negligible; skip it.
        private const float DoseRangeM = 3000f;
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
        private CelestialBody body;
        private LineRenderer line;
        private Vector3[] positions = new Vector3[0];
        private Vector3[] dosePoints = new Vector3[0];
        private bool ownerAlive = true;

        public CelestialBody Body => body;

        public static PlutoExhaustTrail Create(CelestialBody body)
        {
            var go = new GameObject("Pluto exhaust trail");
            var trail = go.AddComponent<PlutoExhaustTrail>();
            trail.body = body;
            trail.line = go.AddComponent<LineRenderer>();
            trail.line.useWorldSpace = true;
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
                trail.line.material = new Material(shader) { mainTexture = Texture2D.whiteTexture };
            else
                trail.line.enabled = false;
            return trail;
        }

        public void Record(Vector3 worldPosition, double powerMW, double now) =>
            model.AddIfDue(PlutoFallingObject.ToLocalPoint(body, worldPosition), powerMW, now);

        // The missile stopped feeding this trail (destroyed, changed body or shut down for good).
        public void Release() => ownerAlive = false;

        private void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || body == null)
            {
                Destroy(gameObject);
                return;
            }
            double now = Planetarium.GetUniversalTime();
            model.Prune(now);
            if (model.Count == 0)
            {
                if (!ownerAlive)
                    Destroy(gameObject);
                return;
            }
            Irradiate(now, TimeWarp.fixedDeltaTime);
        }

        private void Irradiate(double now, double dt)
        {
            PlutoRadiationScenario scenario = PlutoRadiationScenario.Instance;
            if (scenario == null)
                return;
            crewedParts.Clear();
            foreach (Vessel v in FlightGlobals.VesselsLoaded)
            {
                if (v == null || v.mainBody != body)
                    continue;
                foreach (Part p in v.parts)
                {
                    if (p.protoModuleCrew != null && p.protoModuleCrew.Count > 0)
                        crewedParts.Add(p);
                }
            }
            if (crewedParts.Count == 0)
                return;

            int count = model.Count;
            if (dosePoints.Length < count)
                dosePoints = new Vector3[PlutoTrailModel.MaxPoints];
            for (int i = 0; i < count; i++)
                dosePoints[i] = PlutoFallingObject.ToWorldPoint(body, model[i].Position);

            foreach (Part p in crewedParts)
            {
                if (p == null || p.vessel == null)
                    continue;
                Vector3 where = p.transform.position;
                double sv = 0;
                for (int i = 0; i < count; i++)
                {
                    float distance = (dosePoints[i] - where).magnitude;
                    if (distance < DoseRangeM)
                        sv += PlutoRadiation.DoseRate(model.StrengthMW(i, now), distance) * dt;
                }
                if (sv > 0)
                    scenario.DosePart(p.vessel, p, sv);
            }
            scenario.ApplyDeaths();
        }

        private void LateUpdate()
        {
            if (line == null || body == null || !line.enabled)
                return;
            int count = model.Count;
            if (positions.Length != count)
                positions = new Vector3[count];
            for (int i = 0; i < count; i++)
                positions[i] = PlutoFallingObject.ToWorldPoint(body, model[i].Position);
            line.positionCount = count;
            line.SetPositions(positions);
            if (count == 0)
                return;

            // Oldest end faint, newest end strongest; overall strength follows the newest point's age.
            double now = Planetarium.GetUniversalTime();
            float newest = (float)model.Fade(count - 1, now);
            float oldest = (float)model.Fade(0, now);
            line.startColor = new Color(Haze.r, Haze.g, Haze.b, 0.25f * oldest);
            line.endColor = new Color(Haze.r, Haze.g, Haze.b, 0.25f * newest);
        }
    }
}
