using System.Collections.Generic;
using UnityEngine;

namespace ProjectPluto
{
    // One warhead detonation: the scaled Sundial visuals plus destruction of loaded parts in range.
    public static class PlutoBlast
    {
        // Pluto's 1 Mt effect is drawn with a third of Sundial's smoke puffs.
        public const float Quality = 0.35f;
        public const float FarQuality = 0.2f;
        private const float FarCameraM = 20000f;

        public static float SizeScale => (float)(PlutoConstants.FireballRadiusM / 2800.0);

        public static void Detonate(Vector3 center, CelestialBody body, float damageRadius)
        {
            if (body == null)
                return;

            float radiusSquared = damageRadius * damageRadius;
            Vessel watched = PlutoCameraZoom.WatchedVessel();
            bool watchedHit = false;
            var targets = new List<Part>();
            var packedHit = new List<Vessel>();
            foreach (Vessel v in FlightGlobals.VesselsLoaded)
            {
                if (v == null || v.mainBody != body)
                    continue;
                foreach (Part p in v.parts)
                {
                    if (p == null || (p.transform.position - center).sqrMagnitude > radiusSquared)
                        continue;
                    watchedHit |= v == watched;
                    if (v.packed)
                    {
                        // Landed craft and flags stay packed when the missile is a few km away: destroy whole.
                        packedHit.Add(v);
                        break;
                    }
                    targets.Add(p);
                }
            }

            if (watchedHit)
                PlutoCameraZoom.Preserve();
            PlutoDetonationEffect.Spawn(center, body, SizeScale, (float)PlutoConstants.CloudTopM, QualityFor(center));

            // A Pluto caught in the blast breaches (its own warheads do not go off); everything else explodes.
            foreach (Part p in targets)
            {
                if (p == null || p.State == PartStates.DEAD)
                    continue;
                p.FindModuleImplementing<ModulePlutoReactor>()?.TriggerBreach();
                p.explode();
            }
            foreach (Vessel v in packedHit)
            {
                if (v != null && v.state != Vessel.State.DEAD)
                    v.Die();
            }
        }

        // Far-away bursts are drawn with fewer smoke puffs: the detail cannot be seen.
        private static float QualityFor(Vector3 center)
        {
            Camera camera = FlightCamera.fetch != null ? FlightCamera.fetch.mainCamera : null;
            if (camera == null)
                return Quality;
            return (camera.transform.position - center).magnitude > FarCameraM ? FarQuality : Quality;
        }
    }
}
