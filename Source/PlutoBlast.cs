using System.Collections.Generic;
using UnityEngine;

namespace ProjectPluto
{
    // One warhead detonation: the scaled Sundial visuals plus destruction of loaded parts in range.
    public static class PlutoBlast
    {
        // Pluto's 1 Mt effect is drawn with a third of Sundial's smoke puffs.
        public const float Quality = 0.35f;

        public static float SizeScale => (float)(PlutoConstants.FireballRadiusM / 2800.0);

        public static void Detonate(Vector3 center, CelestialBody body, float damageRadius)
        {
            if (body == null)
                return;

            float radiusSquared = damageRadius * damageRadius;
            Vessel watched = PlutoCameraZoom.WatchedVessel();
            bool watchedHit = false;
            var targets = new List<Part>();
            foreach (Vessel v in FlightGlobals.VesselsLoaded)
            {
                if (v == null || v.packed || v.mainBody != body)
                    continue;
                foreach (Part p in v.parts)
                {
                    if (p != null && (p.transform.position - center).sqrMagnitude <= radiusSquared)
                    {
                        targets.Add(p);
                        watchedHit |= v == watched;
                    }
                }
            }

            if (watchedHit)
                PlutoCameraZoom.Preserve();
            PlutoDetonationEffect.Spawn(center, body, SizeScale, (float)PlutoConstants.CloudTopM, Quality);

            // A Pluto caught in the blast breaches (its own warheads do not go off); everything else explodes.
            foreach (Part p in targets)
            {
                if (p == null || p.State == PartStates.DEAD)
                    continue;
                p.FindModuleImplementing<ModulePlutoReactor>()?.TriggerBreach();
                p.explode();
            }
        }
    }
}
