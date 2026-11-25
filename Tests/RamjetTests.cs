namespace ProjectPluto.Tests
{
    public static class RamjetTests
    {
        private const double SeaLevelDensity = 1.225, SeaLevelTemp = 288.15;

        private static PlutoRamjet.Result Run(double powerMW, double density, double speed, double tempK) =>
            PlutoRamjet.Compute(powerMW * 1e6, density, speed, tempK);

        public static void Ramjet_NoThrustWhenStopped()
        {
            Assert.Equal(0, Run(600, SeaLevelDensity, 0, SeaLevelTemp).ThrustN, 1e-6, "stopped");
        }

        public static void Ramjet_NoThrustInVacuum()
        {
            Assert.Equal(0, Run(600, 0, 1500, 200).ThrustN, 1e-6, "vacuum");
        }

        public static void Ramjet_NoThrustWithoutPower()
        {
            Assert.Equal(0, Run(0, SeaLevelDensity, 1020, SeaLevelTemp).ThrustN, 1e-6, "cold reactor");
        }

        // Mach 3 at sea level, full power: about 1,000 kg/s of air heated ~600 K → roughly 280 kN.
        public static void Ramjet_Mach3SeaLevelFullPower()
        {
            var r = Run(600, SeaLevelDensity, 1020, SeaLevelTemp);
            Assert.Equal(1000, r.MassFlowKgS, 20, "mass flow");
            Assert.Equal(280e3, r.ThrustN, 30e3, "thrust");
            Assert.Equal(1, r.CoolingFraction, 1e-9, "air can carry full power away");
        }

        public static void Ramjet_HeatAddedNeverExceedsPower()
        {
            var r = Run(600, SeaLevelDensity, 100, SeaLevelTemp);
            Assert.True(r.HeatAddedW <= 600e6 + 1, "heat ≤ power");
            Assert.True(r.ExitTempK <= PlutoRamjet.CoreTempK + 1e-6, "air no hotter than the core");
        }

        public static void Ramjet_MorePowerMoreThrust()
        {
            Assert.True(Run(600, SeaLevelDensity, 1020, SeaLevelTemp).ThrustN
                > Run(300, SeaLevelDensity, 1020, SeaLevelTemp).ThrustN, "power raises thrust");
        }

        // Ram heating: once the incoming air is as hot as the core, nothing more can be added.
        public static void Ramjet_TopSpeedFromRamHeating()
        {
            var r = Run(600, SeaLevelDensity, 1650, SeaLevelTemp);
            Assert.True(r.StagnationTempK >= PlutoRamjet.CoreTempK, "ram air at core temperature");
            Assert.Equal(0, r.ThrustN, 1e-6, "no thrust");
            Assert.Equal(0, r.CoolingFraction, 1e-9, "no cooling");
        }

        public static void Ramjet_ThinAirLimitsCooling()
        {
            var r = Run(600, 0.3, 1200, 230);
            Assert.True(r.CoolingFraction < 1 && r.CoolingFraction > 0, "partial cooling high up");
        }
    }
}
