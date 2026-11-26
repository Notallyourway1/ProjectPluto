namespace ProjectPluto.Tests
{
    public static class ThrustTests
    {
        private const double LaunchWeightN = 27500 * 9.81;

        private static double SeaLevelThrustN(double mach) =>
            PlutoRamjet.Compute(600e6, 1.225, mach * 340.3, 288.15).ThrustN * PlutoConstants.ThrustMultiplier;

        // Above about Mach 0.8 the ramjet must out-push KSP's drag with a margin, or the missile sinks.
        public static void Thrust_HoldsFlightJustBelowMach1()
        {
            Assert.True(SeaLevelThrustN(0.8) > 0.6 * LaunchWeightN, "Mach 0.8: well over half the weight");
            Assert.True(SeaLevelThrustN(1.0) > 0.85 * LaunchWeightN, "Mach 1: close to the weight");
        }

        public static void Thrust_ExceedsWeightAtRunSpeed()
        {
            Assert.True(SeaLevelThrustN(PlutoConstants.RunMach) > 1.5 * LaunchWeightN, "Mach 3: 1.5 × weight");
        }

        public static void Thrust_StillStopsNearMach5()
        {
            Assert.True(SeaLevelThrustN(4.9) < 0.05 * SeaLevelThrustN(3), "no thrust where ram heat reaches the core");
        }
    }
}
