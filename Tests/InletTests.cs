namespace ProjectPluto.Tests
{
    public static class InletTests
    {
        private static void Fly(PlutoInlet inlet, double mach, double seconds)
        {
            for (int i = 0; i < (int)(seconds / 0.02); i++)
                inlet.Update(0.02, mach);
        }

        public static void Inlet_StartedAtDesignSpeed()
        {
            var inlet = new PlutoInlet();
            Fly(inlet, 4.2, 5);
            Assert.True(inlet.Started, "started at Mach 4.2");
            Assert.Equal(1, inlet.CaptureFactor, 1e-9, "full capture");
        }

        public static void Inlet_UnstartsAboveLimit()
        {
            var inlet = new PlutoInlet();
            Assert.True(inlet.Update(0.02, 4.6) == PlutoInlet.Change.Unstarted, "reports the unstart");
            Assert.True(!inlet.Started, "unstarted");
            Assert.Equal(PlutoConstants.UnstartCapture, inlet.CaptureFactor, 1e-9, "capture collapses");
        }

        public static void Inlet_StaysUnstartedUntilSlowEnough()
        {
            var inlet = new PlutoInlet();
            inlet.Update(0.02, 4.6);
            Fly(inlet, 4.2, 10);
            Assert.True(!inlet.Started, "Mach 4.2 is not slow enough to restart");
        }

        public static void Inlet_RestartsAfterSlowingDown()
        {
            var inlet = new PlutoInlet();
            inlet.Update(0.02, 4.6);
            Fly(inlet, 3.8, 1);
            Assert.True(!inlet.Started, "needs 2 s below the restart speed");
            Fly(inlet, 3.8, 1.5);
            Assert.True(inlet.Started, "restarted");
        }

        public static void Inlet_UnstartedCaptureCutsThrust()
        {
            double full = PlutoRamjet.Compute(600e6, 0.4, 1350, 230).ThrustN;
            double unstarted = PlutoRamjet.Compute(600e6, 0.4, 1350, 230,
                PlutoConstants.IntakeAreaM2 * PlutoConstants.UnstartCapture).ThrustN;
            Assert.True(unstarted < 0.3 * full, "unstart costs most of the thrust");
        }
    }
}
