using System;

namespace ProjectPluto.Tests
{
    public static class RadiationTests
    {
        public static void Radiation_VideoPassIs028Sv()
        {
            double dose = 0, dt = 0.001;
            for (double t = -60; t <= 60; t += dt)
            {
                double x = 1020 * t;
                dose += PlutoRadiation.DoseRate(600, Math.Sqrt(150 * 150 + x * x)) * dt;
            }
            Assert.Equal(0.28, dose, 0.01, "pass dose");
        }

        public static void Radiation_ScalesWithPower()
        {
            Assert.Equal(PlutoRadiation.DoseRate(600, 500) / 2, PlutoRadiation.DoseRate(300, 500), 1e-12, "half power");
        }

        public static void Radiation_ZeroDistanceIsFinite()
        {
            double rate = PlutoRadiation.DoseRate(600, 0);
            Assert.True(!double.IsInfinity(rate) && !double.IsNaN(rate), "finite");
        }

        public static void Radiation_CrossedPicksHighest()
        {
            Assert.True(PlutoRadiation.Crossed(0.05, 7) == PlutoRadiation.Threshold.Lethal, "lethal");
            Assert.True(PlutoRadiation.Crossed(0.2, 0.5) == PlutoRadiation.Threshold.None, "none");
            Assert.True(PlutoRadiation.Crossed(0.09, 0.11) == PlutoRadiation.Threshold.Warn1, "warn1");
            Assert.True(PlutoRadiation.Crossed(0.5, 1.5) == PlutoRadiation.Threshold.Warn2, "warn2");
        }
    }
}
