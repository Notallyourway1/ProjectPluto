namespace ProjectPluto.Tests
{
    public static class RamjetSplitTests
    {
        public static void Ramjet_IntakeThenHeatMatchesOldSinglePass()
        {
            foreach (double speed in new[] { 0.0, 150, 400, 1000, 1600 })
            foreach (double power in new[] { 0.0, 1e8, 6e8 })
            {
                PlutoRamjet.Result r = PlutoRamjet.Intake(1.0, speed, 260);
                double cooling = r.CoolingFraction;
                PlutoRamjet.Heat(ref r, power);
                // Reference: the original one-pass formula.
                double mach2 = speed * speed / (1.4 * 287.05 * 260);
                double tt0 = 260 * (1 + 0.2 * mach2);
                double flow = speed * 0.8;
                double absorb = speed > 0 ? flow * 1005 * System.Math.Max(0, PlutoRamjet.CoreTempK - tt0) : 0;
                double heat = System.Math.Min(power, absorb);
                double thrust = heat > 0
                    ? 0.85 * flow * (speed * System.Math.Sqrt((tt0 + heat / (flow * 1005)) / tt0) - speed) : 0;
                Assert.Equal(thrust, r.ThrustN, 1e-6, $"thrust at {speed} m/s, {power} W");
                Assert.Equal(System.Math.Min(1, absorb / 6e8), cooling, 1e-12, "cooling independent of power");
            }
        }
    }
}
