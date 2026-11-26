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
        // Width, colour and fade are re-sampled twice a second as the trail ages.
        private const float LookIntervalSeconds = 0.5f;
        // Up to this many width/colour keys along the line (Unity gradients take at most 8).
        private const int LookKeys = 8;
        // One billow of the haze texture every this many metres along the trail.
        private const float BillowLengthM = 220f;
        private static Texture2D hazeTexture;
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
        private float lookTimer;
        private readonly Gradient gradient = new Gradient();
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
            trail.line.widthMultiplier = 1f;
            trail.line.numCapVertices = 4;
            trail.line.numCornerVertices = 2;
            trail.line.textureMode = LineTextureMode.Tile;
            trail.line.alignment = LineAlignment.View;
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
                trail.material = new Material(shader) { mainTexture = HazeTexture() };
                trail.material.mainTextureScale = new Vector2(1f / BillowLengthM, 1f);
                trail.line.material = trail.material;
            }
            else
                trail.line.enabled = false;
            return trail;
        }

        // Drops a trail point when due, and keeps the line's head at the nozzle.
        public void Record(Vector3 worldPosition, double powerMW, double now)
        {
            bool added = model.AddIfDue(PlutoFallingObject.ToLocalPoint(body, worldPosition), powerMW, now);
            liveLocal = body.transform.InverseTransformPoint(worldPosition);
            // Only a new point needs the line rebuilt; the head alone just moves.
            if (added || !hasLive)
                drawnCount = -1;
            hasLive = true;
            lastRecord = now;
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
            bool rebuilt = total != drawnCount;
            if (rebuilt)
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

            lookTimer -= Time.deltaTime;
            if (rebuilt || lookTimer <= 0)
            {
                lookTimer = LookIntervalSeconds;
                RefreshLook(Planetarium.GetUniversalTime(), count, total);
            }
        }

        // Samples width, colour and fade along the line from each point's age (oldest end first).
        private void RefreshLook(double now, int count, int total)
        {
            int keys = Mathf.Clamp(total, 2, LookKeys);
            var widths = new Keyframe[keys];
            var colours = new GradientColorKey[keys];
            var alphas = new GradientAlphaKey[keys];
            for (int k = 0; k < keys; k++)
            {
                int index = Mathf.RoundToInt((float)k * (total - 1) / (keys - 1));
                float t = total > 1 ? (float)index / (total - 1) : k;
                double age = index < count ? now - model[index].Time : 0;
                widths[k] = new Keyframe(t, (float)PlutoTrailLook.WidthM(age));
                PlutoTrailLook.Colour(age, out double r, out double g, out double b);
                colours[k] = new GradientColorKey(new Color((float)r, (float)g, (float)b), t);
                alphas[k] = new GradientAlphaKey((float)PlutoTrailLook.Alpha(age), t);
            }
            line.widthCurve = new AnimationCurve(widths);
            gradient.SetKeys(colours, alphas);
            line.colorGradient = gradient;
        }

        // Soft-edged, billowy white texture shared by all trails: alpha falls off across the width and
        // varies gently along the length.
        private static Texture2D HazeTexture()
        {
            if (hazeTexture != null)
                return hazeTexture;
            const int along = 128, across = 32;
            hazeTexture = new Texture2D(along, across, TextureFormat.RGBA32, true)
            {
                name = "Pluto exhaust haze",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[along * across];
            for (int y = 0; y < across; y++)
            {
                float v = (y + 0.5f) / across * 2f - 1f;
                for (int x = 0; x < along; x++)
                {
                    float u = (float)x / along * Mathf.PI * 2f;
                    // Whole-number frequencies so the pattern repeats seamlessly.
                    float billow = 0.72f + 0.16f * Mathf.Sin(u * 2f + 1.3f) + 0.12f * Mathf.Sin(u * 5f + v * 2.1f);
                    float edge = 0.28f + 0.1f * Mathf.Sin(u * 3f + 0.7f);
                    float profile = Mathf.Exp(-(v * v) / (2f * edge * edge));
                    pixels[y * along + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(profile * billow));
                }
            }
            hazeTexture.SetPixels32(pixels);
            hazeTexture.Apply(true, true);
            return hazeTexture;
        }
    }
}
