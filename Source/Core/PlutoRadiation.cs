using System;

namespace ProjectPluto
{
    // Unshielded-reactor dose: falls off with the square of distance, scales with reactor power.
    public static class PlutoRadiation
    {
        public enum Threshold { None, Warn1, Warn2, Lethal }

        // Sv/s at distanceM metres from a reactor producing powerMW.
        public static double DoseRate(double powerMW, double distanceM) =>
            PlutoConstants.DoseK * (powerMW / PlutoConstants.ReactorMaxMW) / Math.Max(distanceM * distanceM, 1);

        // The highest threshold passed when a total dose goes from `before` to `after`.
        public static Threshold Crossed(double before, double after)
        {
            if (before < PlutoConstants.DoseLethal && after >= PlutoConstants.DoseLethal)
                return Threshold.Lethal;
            if (before < PlutoConstants.DoseWarn2 && after >= PlutoConstants.DoseWarn2)
                return Threshold.Warn2;
            if (before < PlutoConstants.DoseWarn1 && after >= PlutoConstants.DoseWarn1)
                return Threshold.Warn1;
            return Threshold.None;
        }
    }
}
