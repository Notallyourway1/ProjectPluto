using System;

namespace ProjectPluto.Tests
{
    public static class OverhaulReviewTests
    {
        // Critical 1: whatever throttle KSP applies, delivered thrust (rating × throttle) never exceeds
        // the physics thrust, and no throttle means no thrust.
        public static void Rating_NeverDeliversMoreThanPhysics()
        {
            foreach (double throttle in new[] { 0.0, 0.004, 0.01, 0.05, 0.1, 0.5, 1.0 })
            {
                double delivered = PlutoRamjet.EngineRatingKN(150e3, throttle) * throttle;
                Assert.True(delivered <= 150 + 1e-9, $"throttle {throttle}: {delivered:0.0} kN ≤ 150 kN");
            }
            Assert.Equal(0, PlutoRamjet.EngineRatingKN(150e3, 0), 1e-9, "zero throttle, zero rating");
        }

        // Critical 2: an inlet unstart at governed power must not SCRAM the reactor.
        public static void Unstart_AtGovernedPowerDoesNotScram()
        {
            var r = new PlutoReactorModel();
            r.Start();
            double cooling = 0.6;
            for (int i = 0; i < 6000; i++)
                r.Update(0.02, PlutoReactorModel.GovernedDemand(1, cooling), cooling);
            Assert.True(r.State == PlutoReactorState.Running, "running before unstart");
            cooling *= PlutoConstants.UnstartCapture;
            for (int i = 0; i < 500; i++)
                r.Update(0.02, PlutoReactorModel.GovernedDemand(1, cooling), cooling);
            Assert.True(r.State == PlutoReactorState.Running, "survives the unstart");
        }

        // Critical 2 (same cause): booster burnout with only partial cooling must not SCRAM either.
        public static void Burnout_WithPartialCoolingDoesNotScram()
        {
            var r = new PlutoReactorModel();
            r.Start();
            for (int i = 0; i < 1000; i++)
                r.Update(0.02, 1, 0.5);   // warming ignores airflow
            Assert.True(r.State == PlutoReactorState.Running, "warm");
            for (int i = 0; i < 1000; i++)
                r.Update(0.02, PlutoReactorModel.GovernedDemand(1, 0.5), 0.5);
            Assert.True(r.State == PlutoReactorState.Running, "governor catches the handover");
        }

        // Important 4: no −273 °C readout at rest or in vacuum.
        public static void Exhaust_AmbientWhenNotFlowing()
        {
            Assert.Equal(288.15, PlutoRamjet.Compute(600e6, 1.225, 0, 288.15).ExitTempK, 1e-9, "at rest");
            Assert.Equal(200, PlutoRamjet.Compute(600e6, 0, 1500, 200).ExitTempK, 1e-9, "vacuum");
        }

        // Improvement: other atmospheres (Kopernicus) use the body's own gas properties.
        public static void Ramjet_UsesGivenGasProperties()
        {
            double air = PlutoRamjet.Compute(600e6, 1.225, 1020, 288.15).ThrustN;
            double sameGas = PlutoRamjet.Compute(600e6, 1.225, 1020, 288.15, PlutoConstants.IntakeAreaM2,
                1.4, 287.05).ThrustN;
            double co2 = PlutoRamjet.Compute(600e6, 1.225, 1020, 288.15, PlutoConstants.IntakeAreaM2,
                1.29, 188.9).ThrustN;
            Assert.Equal(air, sameGas, 1e-6, "defaults are air");
            Assert.True(Math.Abs(co2 - air) > 1000, "different gas, different thrust");
        }
    }
}
