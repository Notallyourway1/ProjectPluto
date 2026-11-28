using System;

namespace ProjectPluto
{
    // Game-independent sound rules: how loud and how muffled a warhead's blast is with distance, when its
    // sound arrives, how fast the Geiger counter clicks, and how loud the ramjet is for its thrust.
    public static class PlutoSoundModel
    {
        // Beyond this the blast is not heard at all.
        public const double MaxHearRangeM = 60000;
        // Distance at which the blast is at half loudness.
        public const double HalfLoudnessM = 3000;
        public const double FallbackSpeedOfSound = 340;
        // Low-pass cutoff: open close by, closing toward a deep rumble far away (and inside a cabin).
        public const double OpenCutoffHz = 22000;
        public const double FarCutoffHz = 350;
        public const double CutoffFalloffM = 6000;
        public const double CabinCutoffHz = 2500;

        // Geiger counter: no clicks below this dose rate; 10 more clicks a second for every tenfold rise.
        public const double GeigerThresholdSvPerS = 1e-6;
        public const double GeigerClicksPerDecade = 10;
        public const double GeigerMaxClicks = 80;

        // Ramjet sound and shock diamonds reach full strength at this many kN of physics thrust (before
        // the thrust multiplier).
        public const double RamjetFullSoundKN = 300;

        // 0..1 loudness of a blast heard distanceM away.
        public static double BoomLoudness(double distanceM)
        {
            if (distanceM >= MaxHearRangeM)
                return 0;
            double near = 1 / (1 + Math.Pow(Math.Max(0, distanceM) / HalfLoudnessM, 1.3));
            // Fade the last fifth of the range to nothing so the cut-off is not heard.
            double edge = Math.Min(1, (MaxHearRangeM - distanceM) / (0.2 * MaxHearRangeM));
            return near * edge;
        }

        public static double BoomCutoffHz(double distanceM, bool inCabin)
        {
            double cutoff = Math.Max(FarCutoffHz, OpenCutoffHz * Math.Exp(-Math.Max(0, distanceM) / CutoffFalloffM));
            return inCabin ? Math.Min(cutoff, CabinCutoffHz) : cutoff;
        }

        // The blast's sound has reached a listener distanceM away once the front, spreading at the speed
        // of sound since the burst, is at least that far out. A listener flying away faster than sound
        // (a Mach 3 missile camera) can outrun it.
        public static bool BoomArrived(double distanceM, double secondsSinceBurst, double speedOfSound) =>
            distanceM <= Math.Max(0, secondsSinceBurst) * SpeedOfSound(speedOfSound);

        // The front has spread past hearing range: nobody can hear it any more.
        public static bool BoomExpired(double secondsSinceBurst, double speedOfSound) =>
            secondsSinceBurst * SpeedOfSound(speedOfSound) > MaxHearRangeM;

        public static double SpeedOfSound(double measured) => measured > 1 ? measured : FallbackSpeedOfSound;

        public static double GeigerClicksPerSecond(double doseRateSvPerS)
        {
            if (!(doseRateSvPerS > GeigerThresholdSvPerS))
                return 0;
            return Math.Min(GeigerMaxClicks, GeigerClicksPerDecade * Math.Log10(doseRateSvPerS / GeigerThresholdSvPerS));
        }

        // 0..1 strength for the ramjet's running sound and exhaust from the thrust it is really making.
        public static double RamjetLevel(double thrustN, double thrustMultiplier)
        {
            double full = RamjetFullSoundKN * 1000 * Math.Max(thrustMultiplier, 0.01);
            double level = thrustN / full;
            return level > 0 ? Math.Min(1, level) : 0;
        }
    }
}
