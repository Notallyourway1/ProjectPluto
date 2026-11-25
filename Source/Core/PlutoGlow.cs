using System;

namespace ProjectPluto
{
    // Approximate incandescent colour of hot metal and air: nothing below ~500 °C, dull red, then orange.
    public static class PlutoGlow
    {
        private const double GlowStartC = 500;
        private const double GlowFullC = 1400;

        public static void ColorForTemp(double tempC, out double r, out double g, out double b)
        {
            double t = Math.Max(0, Math.Min(1, (tempC - GlowStartC) / (GlowFullC - GlowStartC)));
            double brightness = Math.Pow(t, 1.5);
            r = brightness;
            g = brightness * (0.15 + 0.5 * t);
            b = brightness * 0.15 * t * t;
        }
    }
}
