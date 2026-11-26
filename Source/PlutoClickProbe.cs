using System;
using System.Text;
using UnityEngine;

namespace ProjectPluto
{
    // Diagnostic: when the player right-clicks while flying a Pluto missile, writes to KSP.log what the
    // click hit (in order), which part KSP considers hovered, and how long and far the click was.
    // KSP opens a part's menu only if the nearest thing under the mouse is that part and the click is a
    // short tap, so this shows why a click did not open the missile's menu. Costs nothing between clicks.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class PlutoClickProbe : MonoBehaviour
    {
        private const int MaxHits = 6;
        private float downTime;
        private Vector3 downPosition;

        private void Update()
        {
            if (Input.GetMouseButtonDown(1))
            {
                downTime = Time.realtimeSinceStartup;
                downPosition = Input.mousePosition;
            }
            if (!Input.GetMouseButtonUp(1))
                return;
            Vessel active = FlightGlobals.ActiveVessel;
            if (active == null || !HasPluto(active))
                return;
            Camera camera = FlightCamera.fetch != null ? FlightCamera.fetch.mainCamera : null;
            if (camera == null)
                return;

            var log = new StringBuilder("[Pluto] Click probe: ");
            log.AppendFormat("tap {0:0.00} s, moved {1:0} px, {2:0} fps, hovered part: {3}",
                Time.realtimeSinceStartup - downTime, (Input.mousePosition - downPosition).magnitude,
                1f / Math.Max(Time.unscaledDeltaTime, 1e-4f), Mouse.HoveredPart != null ? Mouse.HoveredPart.partInfo.name : "none");
            RaycastHit[] hits = Physics.RaycastAll(camera.ScreenPointToRay(Input.mousePosition), 10000f, ~0,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length && i < MaxHits; i++)
            {
                Collider c = hits[i].collider;
                Part owner = c.GetComponentInParent<Part>();
                log.AppendFormat("\n  {0:0.0} m: {1} (layer {2}{3}) part: {4}", hits[i].distance, Path(c.transform),
                    c.gameObject.layer, c.isTrigger ? ", trigger" : "", owner != null ? owner.partInfo.name : "none");
            }
            if (hits.Length == 0)
                log.Append("\n  nothing under the mouse");
            Debug.Log(log.ToString());
        }

        private static bool HasPluto(Vessel v)
        {
            foreach (Part p in v.parts)
                if (p.partInfo != null && p.partInfo.name == "projectPlutoSLAM")
                    return true;
            return false;
        }

        private static string Path(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null && path.Length < 120; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }
    }
}
