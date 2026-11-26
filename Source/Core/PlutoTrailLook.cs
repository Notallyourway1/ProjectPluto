using System;

namespace ProjectPluto
{
    // How the radioactive exhaust trail looks at a given age: a thin, pale streak behind the nozzle that
    // spreads into a wide blue-grey haze and fades away over its life.
    public static class PlutoTrailLook
    {
        public const double NozzleWidthM = 5;
        public const double SpreadWidthM = 70;
        public const double PeakAlpha = 0.32;
        // The streak thickens into view just behind the plume instead of starting as a hard edge.
        public const double FadeInSeconds = 0.6;
        // Visual fade (slower than the radioactive decay, so the haze lingers as it spreads).
        public const double VisualDecaySeconds = 100;

        // Width grows like a diffusing plume: fast at first, then slower.
        public static double WidthM(double ageSeconds)
        {
            double f = Math.Max(0, Math.Min(1, ageSeconds / PlutoConstants.TrailLifeSeconds));
            return NozzleWidthM + (SpreadWidthM - NozzleWidthM) * Math.Sqrt(f);
        }

        public static double Alpha(double ageSeconds)
        {
            double age = Math.Max(0, ageSeconds);
            double fadeIn = Math.Min(1, age / FadeInSeconds);
            double life = Math.Max(0, 1 - age / PlutoConstants.TrailLifeSeconds);
            return PeakAlpha * fadeIn * Math.Exp(-age / VisualDecaySeconds) * life;
        }

        // Pale warm grey when fresh, cooling to blue-grey haze.
        public static void Colour(double ageSeconds, out double r, out double g, out double b)
        {
            double f = Math.Max(0, Math.Min(1, ageSeconds / 60));
            r = 0.86 + (0.62 - 0.86) * f;
            g = 0.82 + (0.68 - 0.82) * f;
            b = 0.72 + (0.76 - 0.72) * f;
        }
    }
}
