using UnityEngine;

namespace ProjectPluto
{
    // Stock KSP opens a part's menu only when the right button comes back up within 0.2 s of going
    // down. At low frame rates, or with a fast missile on screen, most clicks take a little longer,
    // so the missile's menu seemed not to open. For Pluto parts this accepts a still right-click of
    // up to MaxTapSeconds: a frame after the click, if stock did not open the menu, it opens it.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class PlutoClickHelper : MonoBehaviour
    {
        private const float StockTapSeconds = 0.2f;
        private const float MaxTapSeconds = 0.6f;
        private const float MaxMovePx = 6f;
        private float downTime;
        private Vector3 downPosition;
        private Part pending;

        private void Update()
        {
            // One frame after the click stock has had its turn, so a menu opens at most once.
            if (pending != null)
            {
                UIPartActionController ui = UIPartActionController.Instance;
                if (ui != null && pending.State != PartStates.DEAD && ui.GetItem(pending, false) == null)
                    ui.SpawnPartActionWindow(pending);
                pending = null;
            }

            if (Input.GetMouseButtonDown(1))
            {
                downTime = Time.realtimeSinceStartup;
                downPosition = Input.mousePosition;
            }
            if (!Input.GetMouseButtonUp(1))
                return;
            // Shorter clicks are stock's, including one that closes an open menu.
            float tap = Time.realtimeSinceStartup - downTime;
            if (tap < StockTapSeconds || tap > MaxTapSeconds)
                return;
            if ((Input.mousePosition - downPosition).sqrMagnitude > MaxMovePx * MaxMovePx)
                return;
            Part hovered = Mouse.HoveredPart;
            if (hovered != null && IsPlutoPart(hovered))
                pending = hovered;
        }

        private static bool IsPlutoPart(Part p) =>
            p.partInfo != null && (p.partInfo.name == "projectPlutoSLAM" || p.partInfo.name == "projectPlutoLaunchPad");
    }
}
