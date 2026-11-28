namespace ProjectPluto.Tests
{
    public static class ReactorDemandTests
    {
        public static void Demand_BoostWarmupIsFullPower()
        {
            Assert.Equal(1, PlutoReactorModel.Demand(PlutoReactorState.Warming, true, 0, 0), 1e-12, "boost warm-up");
        }

        public static void Demand_RestartFollowsThrottleAndAir()
        {
            Assert.Equal(0, PlutoReactorModel.Demand(PlutoReactorState.Warming, false, 0, 1), 1e-12, "throttle closed");
            Assert.Equal(0.3, PlutoReactorModel.Demand(PlutoReactorState.Warming, false, 1, 0.3), 1e-12, "air-limited");
            Assert.Equal(0.5, PlutoReactorModel.Demand(PlutoReactorState.Running, true, 0.5, 1), 1e-12, "running");
        }

        // A restart at closed throttle must still reach Running (not stay Warming forever).
        public static void Restart_AtClosedThrottleReachesRunning()
        {
            var m = new PlutoReactorModel();
            m.Start();
            m.Update(0.02, 1, 1);
            m.Scram();
            for (int i = 0; i < 5000 && m.CoreTempC >= PlutoConstants.RestartTempC; i++)
                m.Update(0.02, 0, 1);
            m.Restart();
            m.Update(0.02, PlutoReactorModel.Demand(m.State, false, 0, 1), 1);
            Assert.True(m.State == PlutoReactorState.Running, "running at zero demand");
            Assert.Equal(0, m.PowerFraction, 1e-12, "no power at closed throttle");
        }
    }
}
