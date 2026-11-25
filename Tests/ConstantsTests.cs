using System;

namespace ProjectPluto.Tests
{
    public static class ConstantsTests
    {
        // A Mach 3 (1,020 m/s) pass 150 m overhead at full power gives D = kπ / (h·v) ≈ 0.28 Sv.
        public static void DoseConstantMatchesVideoPass()
        {
            Assert.Equal(0.28, PlutoConstants.DoseK * Math.PI / (150 * 1020), 0.01, "pass dose");
        }
    }
}
